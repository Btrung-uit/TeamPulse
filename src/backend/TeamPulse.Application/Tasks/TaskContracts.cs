using TeamPulse.Domain.Tasks;

namespace TeamPulse.Application.Tasks;

public sealed record TaskListQuery(
    int Page,
    int PageSize,
    string? Search,
    ProjectTaskStatus? Status,
    TaskPriority? Priority,
    Guid? AssigneeUserId,
    Guid? SprintId,
    DateOnly? DueBefore,
    bool IncludeArchived);

public sealed record CreateTaskCommand(
    TaskCode Code,
    string Title,
    string? Description,
    Guid? AssigneeUserId,
    Guid? ReviewerUserId,
    Guid? SprintId,
    TaskPriority Priority,
    DateOnly? DueOn,
    decimal? EstimateHours);

public sealed record UpdateTaskCommand(
    TaskCode Code,
    string Title,
    string? Description,
    Guid? AssigneeUserId,
    Guid? ReviewerUserId,
    Guid? SprintId,
    TaskPriority Priority,
    DateOnly? DueOn,
    decimal? EstimateHours,
    byte[] ExpectedRowVersion);

public sealed record ChangeTaskStatusCommand(
    ProjectTaskStatus Status,
    byte[] ExpectedRowVersion);

public sealed record TaskMemberSummary(
    Guid UserId,
    string DisplayName,
    string Email);

public sealed record TaskSprintSummary(Guid Id, string Name);

public sealed record TaskListItem(
    Guid Id,
    string Code,
    string Title,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueOn,
    decimal? EstimateHours,
    TaskMemberSummary? Assignee,
    TaskMemberSummary? Reviewer,
    TaskSprintSummary? Sprint,
    bool IsArchived,
    DateTimeOffset UpdatedAtUtc,
    byte[] RowVersion);

public sealed record TaskDetails(
    Guid Id,
    Guid WorkspaceId,
    Guid ProjectId,
    string Code,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    ProjectTaskStatus? StatusBeforeBlocked,
    TaskPriority Priority,
    DateOnly? DueOn,
    decimal? EstimateHours,
    TaskMemberSummary? Assignee,
    TaskMemberSummary? Reviewer,
    TaskSprintSummary? Sprint,
    bool IsArchived,
    DateTimeOffset? ArchivedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    byte[] RowVersion);

public sealed record PagedTaskResult(
    IReadOnlyList<TaskListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public enum TaskFailure
{
    None,
    ProjectNotFound,
    ProjectArchived,
    TaskNotFound,
    TaskArchived,
    TaskCodeConflict,
    TaskCodeImmutable,
    AssigneeNotFound,
    ReviewerNotFound,
    SprintNotFound,
    InvalidStatusTransition,
    Forbidden,
    ConcurrencyConflict,
}

public sealed record TaskResult<T>(T? Value, TaskFailure Failure)
{
    public bool Succeeded => Failure == TaskFailure.None;

    public static TaskResult<T> Success(T value) => new(value, TaskFailure.None);

    public static TaskResult<T> Failed(TaskFailure failure) => new(default, failure);
}
