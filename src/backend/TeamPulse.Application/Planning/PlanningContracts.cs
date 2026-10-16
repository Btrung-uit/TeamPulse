using TeamPulse.Domain.Tasks;

namespace TeamPulse.Application.Planning;

public sealed record SaveSprintCommand(
    string Name,
    string? Goal,
    DateOnly StartsOn,
    DateOnly EndsOn);

public sealed record SprintSummary(
    Guid Id,
    Guid WorkspaceId,
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int TaskCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TaskSprintAssignmentSummary(
    Guid TaskId,
    string TaskCode,
    Guid? SprintId,
    string? SprintName);

public sealed record DependencyTaskSummary(
    Guid Id,
    string Code,
    string Title,
    ProjectTaskStatus Status,
    DateOnly? DueOn,
    bool IsArchived);

public sealed record DependencyRiskSummary(
    DependencyRiskKind Kind,
    string Code,
    string Message);

public sealed record TaskDependencySummary(
    DependencyTaskSummary Predecessor,
    DependencyTaskSummary Successor,
    bool IsAtRisk,
    IReadOnlyList<DependencyRiskSummary> Risks,
    DateTimeOffset CreatedAtUtc);

public enum PlanningFailure
{
    None,
    ProjectNotFound,
    ProjectArchived,
    SprintNotFound,
    SprintNameConflict,
    SprintHasTasks,
    TaskNotFound,
    TaskArchived,
    SelfDependency,
    DependencyCycle,
}

public sealed record PlanningResult<T>(T? Value, PlanningFailure Failure)
{
    public bool Succeeded => Failure == PlanningFailure.None;

    public static PlanningResult<T> Success(T value) => new(value, PlanningFailure.None);

    public static PlanningResult<T> Failed(PlanningFailure failure) => new(default, failure);
}
