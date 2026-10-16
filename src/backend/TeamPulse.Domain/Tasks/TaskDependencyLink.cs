namespace TeamPulse.Domain.Tasks;

public readonly record struct TaskDependencyLink(
    Guid PredecessorTaskId,
    Guid SuccessorTaskId);
