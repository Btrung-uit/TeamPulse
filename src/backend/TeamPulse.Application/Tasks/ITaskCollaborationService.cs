namespace TeamPulse.Application.Tasks;

public interface ITaskCollaborationService
{
    Task<TaskResult<TaskCollaborationDetails>> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskEvidenceItem>> AddEvidenceAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        AddTaskEvidenceCommand command,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskReviewResult>> ReviewAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        ReviewTaskCommand command,
        CancellationToken cancellationToken = default);
}
