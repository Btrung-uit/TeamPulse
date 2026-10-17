namespace TeamPulse.Application.Tasks;

public interface ITaskService
{
    Task<TaskResult<PagedTaskResult>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        TaskListQuery query,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskDetails>> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskDetails>> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        CreateTaskCommand command,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskDetails>> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        UpdateTaskCommand command,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskDetails>> ChangeStatusAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        ChangeTaskStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskDetails>> ArchiveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default);
}
