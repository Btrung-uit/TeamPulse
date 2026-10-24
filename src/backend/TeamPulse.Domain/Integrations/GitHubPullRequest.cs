using TeamPulse.Domain.Common;
using TeamPulse.Domain.Projects;

namespace TeamPulse.Domain.Integrations;

public sealed class GitHubPullRequest : AuditableEntity
{
    public const int RepositoryNameMaximumLength = 240;
    public const int TitleMaximumLength = 300;
    public const int UrlMaximumLength = 2_048;
    public const int LoginMaximumLength = 100;
    public const int RequestedReviewersMaximumLength = 500;

    private GitHubPullRequest()
    {
    }

    public GitHubPullRequest(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        string repositoryFullName,
        int number,
        string title,
        string url,
        string? authorLogin,
        string? requestedReviewers,
        GitHubPullRequestStatus status,
        DateTimeOffset openedAtUtc,
        DateTimeOffset lastActivityAtUtc)
        : base(id)
    {
        if (workspaceId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new ArgumentException("Pull request scope identifiers cannot be empty.");
        }

        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Pull request number must be positive.");
        }

        WorkspaceId = workspaceId;
        ProjectId = projectId;
        RepositoryFullName = DomainText.Required(
            repositoryFullName,
            nameof(repositoryFullName),
            RepositoryNameMaximumLength);
        Number = number;
        CheckStatus = GitHubCheckStatus.Unknown;
        OpenedAtUtc = openedAtUtc;
        Update(title, url, authorLogin, requestedReviewers, status, lastActivityAtUtc);
    }

    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string RepositoryFullName { get; private set; } = string.Empty;
    public int Number { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? AuthorLogin { get; private set; }
    public string? RequestedReviewers { get; private set; }
    public GitHubPullRequestStatus Status { get; private set; }
    public GitHubCheckStatus CheckStatus { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }
    public Project Project { get; private set; } = null!;
    public ICollection<GitHubPullRequestTask> TaskLinks { get; } = [];

    public void Update(
        string title,
        string url,
        string? authorLogin,
        string? requestedReviewers,
        GitHubPullRequestStatus status,
        DateTimeOffset lastActivityAtUtc)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown pull request status.");
        }

        var normalizedUrl = DomainText.Required(url, nameof(url), UrlMaximumLength);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Pull request URL must be an absolute GitHub HTTPS URL.", nameof(url));
        }

        Title = DomainText.Required(title, nameof(title), TitleMaximumLength);
        Url = normalizedUrl;
        AuthorLogin = DomainText.Optional(authorLogin, nameof(authorLogin), LoginMaximumLength);
        RequestedReviewers = DomainText.Optional(
            requestedReviewers,
            nameof(requestedReviewers),
            RequestedReviewersMaximumLength);
        Status = status;
        LastActivityAtUtc = lastActivityAtUtc;
    }

    public void UpdateChecks(GitHubCheckStatus status, DateTimeOffset lastActivityAtUtc)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown check status.");
        }

        CheckStatus = status;
        LastActivityAtUtc = lastActivityAtUtc;
    }
}
