using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Application.Dashboard;

public sealed record DashboardKpis(
    int TotalTasks,
    int DoneTasks,
    int OpenTasks,
    int InReviewTasks,
    int BlockedTasks,
    int DueSoonTasks,
    int ProgressPercent);

public sealed record DashboardStatusCount(ProjectTaskStatus Status, int Count);

public sealed record DashboardWorkloadItem(
    Guid UserId,
    string DisplayName,
    WorkspaceRole Role,
    int OpenTasks,
    int BlockedTasks,
    int DueSoonTasks);

public sealed record DashboardPriorityTask(
    Guid Id,
    string Code,
    string Title,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueOn);

public sealed record DashboardActivityItem(
    Guid Id,
    Guid ActorUserId,
    string ActorDisplayName,
    ActivityAction Action,
    Guid EntityId,
    string? TaskCode,
    string? PreviousValue,
    string? NewValue,
    DateTimeOffset OccurredAtUtc);

public sealed record DashboardContributionItem(
    Guid UserId,
    string DisplayName,
    WorkspaceRole Role,
    int AcceptedTasks,
    int AcceptedEvidence,
    int Reviews,
    DateTimeOffset? LastContributedAtUtc);

public sealed record ProjectDashboard(
    DashboardKpis Kpis,
    IReadOnlyList<DashboardStatusCount> StatusBreakdown,
    IReadOnlyList<DashboardWorkloadItem> Workload,
    IReadOnlyList<DashboardPriorityTask> MyPriorityTasks,
    IReadOnlyList<DashboardActivityItem> RecentActivity,
    IReadOnlyList<DashboardContributionItem> Contributions);

public enum DashboardFailure
{
    None,
    ProjectNotFound,
}

public sealed record DashboardResult<T>(T? Value, DashboardFailure Failure)
{
    public bool Succeeded => Failure == DashboardFailure.None;
    public static DashboardResult<T> Success(T value) => new(value, DashboardFailure.None);
    public static DashboardResult<T> Failed(DashboardFailure failure) => new(default, failure);
}
