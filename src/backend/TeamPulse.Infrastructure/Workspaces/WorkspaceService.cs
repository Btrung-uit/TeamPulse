using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TeamPulse.Application.Workspaces;
using TeamPulse.Application.Notifications;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Workspaces;

internal sealed class WorkspaceService(
    TeamPulseDbContext dbContext,
    ITransactionalEmailSender emailSender,
    TimeProvider timeProvider) : IWorkspaceService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

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

    public async Task<WorkspaceResult<bool>> InviteAsync(
        Guid workspaceId,
        Guid actorUserId,
        string email,
        WorkspaceRole role,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var activeMemberExists = await dbContext.WorkspaceMembers
            .AnyAsync(member =>
                member.WorkspaceId == workspaceId &&
                member.IsActive &&
                member.User.NormalizedEmail == normalizedEmail,
                cancellationToken);
        if (activeMemberExists)
        {
            return WorkspaceResult<bool>.Failed(WorkspaceFailure.AlreadyMember);
        }

        var workspaceName = await dbContext.Workspaces
            .Where(workspace => workspace.Id == workspaceId)
            .Select(workspace => workspace.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (workspaceName is null)
        {
            return WorkspaceResult<bool>.Failed(WorkspaceFailure.WorkspaceNotFound);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(token);
        var expiresAtUtc = timeProvider.GetUtcNow().Add(InvitationLifetime);
        var invitation = await dbContext.WorkspaceInvitations.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (invitation is null)
        {
            invitation = new WorkspaceInvitation(
                Guid.NewGuid(),
                workspaceId,
                actorUserId,
                email.Trim(),
                role,
                tokenHash,
                expiresAtUtc);
            dbContext.WorkspaceInvitations.Add(invitation);
        }
        else
        {
            invitation.Renew(email.Trim(), role, tokenHash, expiresAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await emailSender.SendWorkspaceInvitationAsync(
            invitation.Email,
            workspaceName,
            token,
            cancellationToken);
        return WorkspaceResult<bool>.Success(true);
    }

    public async Task<WorkspaceResult<WorkspaceSummary>> AcceptInvitationAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var invitation = await dbContext.WorkspaceInvitations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        var user = await dbContext.UserProfiles.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (invitation is null ||
            user is null ||
            !invitation.CanBeAcceptedAt(now) ||
            !string.Equals(
                invitation.NormalizedEmail,
                user.NormalizedEmail,
                StringComparison.Ordinal))
        {
            return WorkspaceResult<WorkspaceSummary>.Failed(WorkspaceFailure.InvalidInvitation);
        }

        var membership = await dbContext.WorkspaceMembers
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == invitation.WorkspaceId &&
                candidate.UserId == userId,
            cancellationToken);
        if (membership is { IsActive: true })
        {
            return WorkspaceResult<WorkspaceSummary>.Failed(WorkspaceFailure.AlreadyMember);
        }

        if (membership is null)
        {
            membership = new WorkspaceMember(invitation.WorkspaceId, userId, invitation.Role);
            dbContext.WorkspaceMembers.Add(membership);
        }
        else
        {
            membership.ChangeRole(invitation.Role);
            membership.Reactivate();
        }

        invitation.Accept(now);
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            invitation.WorkspaceId,
            userId,
            ActivityAction.WorkspaceMemberAdded,
            "WorkspaceMember",
            userId,
            now,
            userId,
            newValue: invitation.Role.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        var workspaceName = await dbContext.Workspaces
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Id == invitation.WorkspaceId)
            .Select(candidate => candidate.Name)
            .SingleAsync(cancellationToken);
        var memberCount = await dbContext.WorkspaceMembers
            .IgnoreQueryFilters()
            .CountAsync(
            candidate => candidate.WorkspaceId == invitation.WorkspaceId && candidate.IsActive,
            cancellationToken);
        return WorkspaceResult<WorkspaceSummary>.Success(new WorkspaceSummary(
            invitation.WorkspaceId,
            workspaceName,
            invitation.Role,
            memberCount));
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static WorkspaceMemberDetails ToDetails(WorkspaceMember membership) =>
        new(
            membership.UserId,
            membership.User.Email,
            membership.User.DisplayName,
            membership.Role);
}
