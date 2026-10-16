namespace TeamPulse.Domain.Tasks;

public enum ProjectTaskStatus
{
    Todo = 1,
    InProgress = 2,
    EvidenceLinked = 3,
    InReview = 4,
    NeedsChanges = 5,
    Blocked = 6,
    Done = 7,
}
