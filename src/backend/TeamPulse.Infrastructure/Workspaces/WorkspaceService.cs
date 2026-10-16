using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Workspaces;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Workspaces;

internal sealed class WorkspaceService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : IWorkspaceService
{
    public async Task<IReadOnlyList<WorkspaceSummary>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = dbContext.WorkspaceMembers.IgnoreQueryFilters();
        var workspaces = dbContext.Workspaces.IgnoreQueryFilters();

        return await (
            from membership in memberships
            join workspace in workspaces on membership.WorkspaceId equals workspace.Id
            where membership.UserId == userId && membership.IsActive
            orderby workspace.Name
            select new WorkspaceSummary(
                workspace.Id,
                workspace.Name,
                membership.Role,
                memberships.Count(candidate =>
                    candidate.WorkspaceId == workspace.Id && candidate.IsActive)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceSummary> CreateAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var workspace = new Workspace(Guid.NewGuid(), name, userId);
        var membership = new WorkspaceMember(workspace.Id, userId, WorkspaceRole.Leader);
        var now = timeProvider.GetUtcNow();

        dbContext.Workspaces.Add(workspace);
        dbContext.WorkspaceMembers.Add(membership);
        dbContext.ActivityLogs.AddRange(
            new ActivityLog(
                Guid.NewGuid(),
                workspace.Id,
                userId,
                ActivityAction.WorkspaceCreated,
                "Workspace",
                workspace.Id,
                now,
                newValue: workspace.Name),
            new ActivityLog(
                Guid.NewGuid(),
                workspace.Id,
                userId,
                ActivityAction.WorkspaceMemberAdded,
                "WorkspaceMember",
                userId,
                now,
                userId,
                newValue: WorkspaceRole.Leader.ToString()));

        await dbContext.SaveChangesAsync(cancellationToken);
        return new WorkspaceSummary(workspace.Id, workspace.Name, WorkspaceRole.Leader, 1);
    }

    public async Task<WorkspaceSummary?> GetAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(membership =>
                membership.WorkspaceId == workspaceId &&
                membership.UserId == userId &&
                membership.IsActive)
            .Select(membership => new WorkspaceSummary(
                membership.WorkspaceId,
                membership.Workspace.Name,
                membership.Role,
                membership.Workspace.Members.Count(candidate => candidate.IsActive)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMemberDetails>> ListMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(membership => membership.WorkspaceId == workspaceId && membership.IsActive)
            .OrderBy(membership => membership.User.DisplayName)
            .Select(membership => new WorkspaceMemberDetails(
                membership.UserId,
                membership.User.Email,
                membership.User.DisplayName,
                membership.Role))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceResult<WorkspaceMemberDetails>> AddMemberAsync(
        Guid workspaceId,
        Guid actorUserId,
        string email,
        WorkspaceRole role,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.UserProfiles.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return WorkspaceResult<WorkspaceMemberDetails>.Failed(WorkspaceFailure.UserNotFound);
        }

        var membership = await dbContext.WorkspaceMembers.SingleOrDefaultAsync(
            candidate => candidate.WorkspaceId == workspaceId && candidate.UserId == user.Id,
            cancellationToken);

        if (membership is { IsActive: true })
        {
            return WorkspaceResult<WorkspaceMemberDetails>.Failed(WorkspaceFailure.AlreadyMember);
        }

        if (membership is null)
        {
            membership = new WorkspaceMember(workspaceId, user.Id, role);
            dbContext.WorkspaceMembers.Add(membership);
        }
        else
        {
            membership.ChangeRole(role);
            membership.Reactivate();
        }

        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            ActivityAction.WorkspaceMemberAdded,
            "WorkspaceMember",
            user.Id,
            timeProvider.GetUtcNow(),
            user.Id,
            newValue: role.ToString()));

        await dbContext.SaveChangesAsync(cancellationToken);
        return WorkspaceResult<WorkspaceMemberDetails>.Success(
            new WorkspaceMemberDetails(user.Id, user.Email, user.DisplayName, role));
    }

    public async Task<WorkspaceResult<WorkspaceMemberDetails>> ChangeMemberRoleAsync(
        Guid workspaceId,
        Guid actorUserId,
        Guid memberUserId,
        WorkspaceRole role,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.WorkspaceMembers
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.UserId == memberUserId &&
                candidate.IsActive,
                cancellationToken);

        if (membership is null)
        {
            return WorkspaceResult<WorkspaceMemberDetails>.Failed(
                WorkspaceFailure.WorkspaceNotFound);
        }

        var previousRole = membership.Role;
        if (previousRole == role)
        {
            return WorkspaceResult<WorkspaceMemberDetails>.Success(ToDetails(membership));
        }

        if (previousRole == WorkspaceRole.Leader && role != WorkspaceRole.Leader)
        {
            var activeLeaderCount = await dbContext.WorkspaceMembers.CountAsync(
                candidate =>
                    candidate.WorkspaceId == workspaceId &&
                    candidate.IsActive &&
                    candidate.Role == WorkspaceRole.Leader,
                cancellationToken);

            if (activeLeaderCount == 1)
            {
                return WorkspaceResult<WorkspaceMemberDetails>.Failed(WorkspaceFailure.LastLeader);
            }
        }

        membership.ChangeRole(role);
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            ActivityAction.WorkspaceMemberRoleChanged,
            "WorkspaceMember",
            memberUserId,
            timeProvider.GetUtcNow(),
            memberUserId,
            previousRole.ToString(),
            role.ToString()));

        await dbContext.SaveChangesAsync(cancellationToken);
        return WorkspaceResult<WorkspaceMemberDetails>.Success(ToDetails(membership));
    }

    private static WorkspaceMemberDetails ToDetails(WorkspaceMember membership) =>
        new(
            membership.UserId,
            membership.User.Email,
            membership.User.DisplayName,
            membership.Role);
}
