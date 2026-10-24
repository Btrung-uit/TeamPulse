using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Dashboard;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Dashboard;

internal sealed class DashboardService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : IDashboardService
{
    private const int PriorityTaskLimit = 5;
    private const int RecentActivityLimit = 8;

    public async Task<DashboardResult<ProjectDashboard>> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Projects.AnyAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken))
        {
            return DashboardResult<ProjectDashboard>.Failed(DashboardFailure.ProjectNotFound);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var dueSoonCutoff = today.AddDays(7);
        var taskEntities = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.WorkspaceId == workspaceId && task.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var tasks = taskEntities
            .Select(task => new TaskProjection(
                task.Id,
                task.Code.Value,
                task.Title,
                task.Status,
                task.Priority,
                task.DueOn,
                task.AssigneeUserId,
                task.IsArchived,
                task.UpdatedAtUtc))
            .ToList();
        var activeTasks = tasks.Where(task => !task.IsArchived).ToList();
        var openTasks = activeTasks
            .Where(task => task.Status != ProjectTaskStatus.Done)
            .ToList();
        var doneCount = activeTasks.Count(task => task.Status == ProjectTaskStatus.Done);
        var dueSoonCount = openTasks.Count(task =>
            task.DueOn >= today && task.DueOn <= dueSoonCutoff);
        var kpis = new DashboardKpis(
            activeTasks.Count,
            doneCount,
            openTasks.Count,
            activeTasks.Count(task => task.Status == ProjectTaskStatus.InReview),
            activeTasks.Count(task => task.Status == ProjectTaskStatus.Blocked),
            dueSoonCount,
            activeTasks.Count == 0
                ? 0
                : (int)Math.Round(doneCount * 100d / activeTasks.Count));

        var statusBreakdown = Enum.GetValues<ProjectTaskStatus>()
            .Select(status => new DashboardStatusCount(
                status,
                activeTasks.Count(task => task.Status == status)))
            .ToArray();

        var members = await dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == workspaceId &&
                member.IsActive &&
                member.Role != WorkspaceRole.Advisor)
            .Include(member => member.User)
            .OrderBy(member => member.User.DisplayName)
            .ToListAsync(cancellationToken);
        var workload = members
            .Select(member =>
            {
                var assigned = openTasks
                    .Where(task => task.AssigneeUserId == member.UserId)
                    .ToList();
                return new DashboardWorkloadItem(
                    member.UserId,
                    member.User.DisplayName,
                    member.Role,
                    assigned.Count,
                    assigned.Count(task => task.Status == ProjectTaskStatus.Blocked),
                    assigned.Count(task => task.DueOn >= today && task.DueOn <= dueSoonCutoff));
            })
            .ToArray();

        var myPriorityTasks = openTasks
            .Where(task => task.AssigneeUserId == currentUserId)
            .OrderByDescending(task => task.Status == ProjectTaskStatus.Blocked)
            .ThenByDescending(task => task.DueOn.HasValue && task.DueOn < today)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.DueOn == null)
            .ThenBy(task => task.DueOn)
            .Take(PriorityTaskLimit)
            .Select(task => new DashboardPriorityTask(
                task.Id,
                task.Code,
                task.Title,
                task.Status,
                task.Priority,
                task.DueOn))
            .ToArray();

        var taskById = tasks.ToDictionary(task => task.Id);
        var taskIds = taskById.Keys.ToArray();
        var activities = await dbContext.ActivityLogs
            .AsNoTracking()
            .Where(activity =>
                activity.WorkspaceId == workspaceId &&
                ((activity.EntityType == nameof(ProjectTask) && taskIds.Contains(activity.EntityId)) ||
                 (activity.EntityType == nameof(Project) && activity.EntityId == projectId)))
            .Include(activity => activity.ActorUser)
            .OrderByDescending(activity => activity.OccurredAtUtc)
            .Take(RecentActivityLimit)
            .ToListAsync(cancellationToken);
        var recentActivity = activities
            .Select(activity => new DashboardActivityItem(
                activity.Id,
                activity.ActorUserId,
                activity.ActorUser?.DisplayName ?? "GitHub automation",
                activity.Action,
                activity.EntityId,
                taskById.GetValueOrDefault(activity.EntityId)?.Code,
                activity.PreviousValue,
                activity.NewValue,
                activity.OccurredAtUtc))
            .ToArray();

        var acceptedEvidence = await dbContext.TaskEvidence
            .AsNoTracking()
            .Where(evidence =>
                evidence.WorkspaceId == workspaceId &&
                evidence.ProjectId == projectId &&
                evidence.SubmittedByUserId.HasValue &&
                evidence.Task.Status == ProjectTaskStatus.Done)
            .Select(evidence => new ContributionEvent(
                evidence.SubmittedByUserId!.Value,
                evidence.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var reviews = await dbContext.TaskComments
            .AsNoTracking()
            .Where(comment =>
                comment.WorkspaceId == workspaceId &&
                comment.ProjectId == projectId)
            .Select(comment => new ContributionEvent(
                comment.AuthorUserId,
                comment.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var acceptedTasks = tasks
            .Where(task => task.Status == ProjectTaskStatus.Done && task.AssigneeUserId.HasValue)
            .ToList();
        var contributions = members
            .Select(member =>
            {
                var memberTasks = acceptedTasks
                    .Where(task => task.AssigneeUserId == member.UserId)
                    .ToList();
                var memberEvidence = acceptedEvidence
                    .Where(item => item.UserId == member.UserId)
                    .ToList();
                var memberReviews = reviews
                    .Where(item => item.UserId == member.UserId)
                    .ToList();
                var timestamps = memberTasks.Select(task => task.UpdatedAtUtc)
                    .Concat(memberEvidence.Select(item => item.OccurredAtUtc))
                    .Concat(memberReviews.Select(item => item.OccurredAtUtc))
                    .ToList();
                return new DashboardContributionItem(
                    member.UserId,
                    member.User.DisplayName,
                    member.Role,
                    memberTasks.Count,
                    memberEvidence.Count,
                    memberReviews.Count,
                    timestamps.Count == 0 ? null : timestamps.Max());
            })
            .OrderByDescending(item => item.AcceptedTasks)
            .ThenByDescending(item => item.AcceptedEvidence)
            .ThenByDescending(item => item.Reviews)
            .ThenBy(item => item.DisplayName)
            .ToArray();

        return DashboardResult<ProjectDashboard>.Success(new(
            kpis,
            statusBreakdown,
            workload,
            myPriorityTasks,
            recentActivity,
            contributions));
    }

    private sealed record TaskProjection(
        Guid Id,
        string Code,
        string Title,
        ProjectTaskStatus Status,
        TaskPriority Priority,
        DateOnly? DueOn,
        Guid? AssigneeUserId,
        bool IsArchived,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ContributionEvent(Guid UserId, DateTimeOffset OccurredAtUtc);
}
