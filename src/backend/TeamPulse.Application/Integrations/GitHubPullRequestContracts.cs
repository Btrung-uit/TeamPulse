using TeamPulse.Domain.Integrations;

namespace TeamPulse.Application.Integrations;

public enum GitHubPullRequestFilter
{
    All,
    Waiting,
    Changes,
    Merged,
}

public sealed record GitHubPullRequestReviewer(
    Guid UserId,
    string DisplayName,
    string Email);

public sealed record GitHubPullRequestTaskItem(
    Guid Id,
    string Code,
    string Title,
    GitHubPullRequestReviewer? Reviewer);

public sealed record GitHubPullRequestItem(
    Guid Id,
    string RepositoryFullName,
    int Number,
    string Title,
    string Url,
    string? AuthorLogin,
    IReadOnlyList<string> RequestedReviewers,
    GitHubPullRequestStatus Status,
    GitHubCheckStatus CheckStatus,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    IReadOnlyList<GitHubPullRequestTaskItem> Tasks);

public sealed record GitHubPullRequestFeed(
    IReadOnlyList<GitHubPullRequestItem> Items,
    int WaitingCount,
    int ChangesCount,
    int MergedCount);
