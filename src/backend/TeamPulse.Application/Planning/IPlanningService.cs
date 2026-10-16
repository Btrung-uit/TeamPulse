namespace TeamPulse.Application.Planning;

public interface IPlanningService
{
    Task<PlanningResult<IReadOnlyList<SprintSummary>>> ListSprintsAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<SprintSummary>> CreateSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        SaveSprintCommand command,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<SprintSummary>> UpdateSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        Guid actorUserId,
        SaveSprintCommand command,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<bool>> DeleteSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<TaskSprintAssignmentSummary>> AssignTaskToSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid? sprintId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<IReadOnlyList<TaskDependencySummary>>> ListDependenciesAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<TaskDependencySummary>> CreateDependencyAsync(
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<PlanningResult<bool>> DeleteDependencyAsync(
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
