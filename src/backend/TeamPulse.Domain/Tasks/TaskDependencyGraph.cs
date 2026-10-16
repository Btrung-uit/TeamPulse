namespace TeamPulse.Domain.Tasks;

public static class TaskDependencyGraph
{
    public static bool WouldCreateCycle(
        IEnumerable<TaskDependencyLink> existingLinks,
        Guid predecessorTaskId,
        Guid successorTaskId)
    {
        EnsureRequiredId(predecessorTaskId, nameof(predecessorTaskId));
        EnsureRequiredId(successorTaskId, nameof(successorTaskId));

        if (predecessorTaskId == successorTaskId)
        {
            return true;
        }

        var successorsByPredecessor = existingLinks
            .GroupBy(link => link.PredecessorTaskId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.SuccessorTaskId).ToArray());
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(successorTaskId);

        while (pending.TryPop(out var currentTaskId))
        {
            if (currentTaskId == predecessorTaskId)
            {
                return true;
            }

            if (!visited.Add(currentTaskId) ||
                !successorsByPredecessor.TryGetValue(currentTaskId, out var successors))
            {
                continue;
            }

            foreach (var successor in successors)
            {
                pending.Push(successor);
            }
        }

        return false;
    }

    private static void EnsureRequiredId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Task identifier is required.", parameterName);
        }
    }
}
