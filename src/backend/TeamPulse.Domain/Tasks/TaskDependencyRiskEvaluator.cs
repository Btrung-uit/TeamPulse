namespace TeamPulse.Domain.Tasks;

public static class TaskDependencyRiskEvaluator
{
    public static IReadOnlyList<DependencyRiskKind> Evaluate(
        ProjectTask predecessor,
        ProjectTask successor,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(successor);

        if (predecessor.WorkspaceId != successor.WorkspaceId ||
            predecessor.ProjectId != successor.ProjectId)
        {
            throw new ArgumentException(
                "Dependency tasks must belong to the same workspace and project.",
                nameof(successor));
        }

        if (predecessor.Id == successor.Id)
        {
            throw new ArgumentException("A task cannot depend on itself.", nameof(successor));
        }

        var risks = new List<DependencyRiskKind>();
        if (predecessor.Status != ProjectTaskStatus.Done)
        {
            risks.Add(DependencyRiskKind.PredecessorIncomplete);

            if (predecessor.DueOn < today)
            {
                risks.Add(DependencyRiskKind.PredecessorOverdue);
            }
        }

        if (predecessor.DueOn.HasValue &&
            successor.DueOn.HasValue &&
            predecessor.DueOn.Value > successor.DueOn.Value)
        {
            risks.Add(DependencyRiskKind.DeadlineConflict);
        }

        return risks;
    }
}
