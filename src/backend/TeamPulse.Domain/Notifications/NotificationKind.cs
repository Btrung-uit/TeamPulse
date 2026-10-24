namespace TeamPulse.Domain.Notifications;

public enum NotificationKind
{
    Assignment,
    ReviewRequest,
    ReviewAccepted,
    ChangesRequested,
    PullRequestReviewRequested,
    PullRequestChangesRequested,
    PullRequestChecksFailed,
    PullRequestMerged,
}
