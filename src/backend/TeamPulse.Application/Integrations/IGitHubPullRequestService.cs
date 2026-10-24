namespace TeamPulse.Application.Integrations;

public interface IGitHubPullRequestService
{
    Task<GitHubPullRequestFeed> ListAsync(
        Guid workspaceId,
        Guid projectId,
        GitHubPullRequestFilter filter,
        CancellationToken cancellationToken = default);
}
