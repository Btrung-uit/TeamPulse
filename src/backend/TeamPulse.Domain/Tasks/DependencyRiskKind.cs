namespace TeamPulse.Domain.Tasks;

public enum DependencyRiskKind
{
    PredecessorIncomplete = 1,
    PredecessorOverdue = 2,
    DeadlineConflict = 3,
}
