using TeamPulse.Domain.Common;

namespace TeamPulse.Domain.Tasks;

public sealed class TaskDependency : IAuditableEntity
{
    private TaskDependency()
    {
    }

    public TaskDependency(
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId)
    {
        EnsureRequiredId(workspaceId, nameof(workspaceId));
        EnsureRequiredId(projectId, nameof(projectId));
        EnsureRequiredId(predecessorTaskId, nameof(predecessorTaskId));
        EnsureRequiredId(successorTaskId, nameof(successorTaskId));

        if (predecessorTaskId == successorTaskId)
        {
            throw new ArgumentException(
                "A task cannot depend on itself.",
                nameof(successorTaskId));
        }

        WorkspaceId = workspaceId;
        ProjectId = projectId;
        PredecessorTaskId = predecessorTaskId;
        SuccessorTaskId = successorTaskId;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid PredecessorTaskId { get; private set; }

    public Guid SuccessorTaskId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public ProjectTask PredecessorTask { get; private set; } = null!;

    public ProjectTask SuccessorTask { get; private set; } = null!;

    private static void EnsureRequiredId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier is required.", parameterName);
        }
    }
}
