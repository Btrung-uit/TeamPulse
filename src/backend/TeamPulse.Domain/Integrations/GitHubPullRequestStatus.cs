namespace TeamPulse.Domain.Integrations;

public enum GitHubPullRequestStatus
{
    Draft,
    WaitingForReview,
    ChangesRequested,
    ReadyToMerge,
    Merged,
    Closed,
}
