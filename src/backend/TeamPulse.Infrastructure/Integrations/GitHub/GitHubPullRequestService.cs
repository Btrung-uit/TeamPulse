using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Integrations;
using TeamPulse.Domain.Integrations;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Integrations.GitHub;

internal sealed class GitHubPullRequestService(TeamPulseDbContext dbContext)
    : IGitHubPullRequestService
{
    public async Task<GitHubPullRequestFeed> ListAsync(
        Guid workspaceId,
        Guid projectId,
        GitHubPullRequestFilter filter,
        CancellationToken cancellationToken = default)
    {
        var scoped = dbContext.GitHubPullRequests
            .AsNoTracking()
            .Where(pullRequest =>
                pullRequest.WorkspaceId == workspaceId &&
                pullRequest.ProjectId == projectId);
        var counts = await scoped
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Waiting = group.Count(pullRequest =>
                    pullRequest.Status == GitHubPullRequestStatus.Draft ||
                    pullRequest.Status == GitHubPullRequestStatus.WaitingForReview ||
                    pullRequest.Status == GitHubPullRequestStatus.ReadyToMerge),
                Changes = group.Count(pullRequest =>
                    pullRequest.Status == GitHubPullRequestStatus.ChangesRequested),
                Merged = group.Count(pullRequest =>
                    pullRequest.Status == GitHubPullRequestStatus.Merged),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var filtered = filter switch
        {
            GitHubPullRequestFilter.Waiting => scoped.Where(pullRequest =>
                pullRequest.Status == GitHubPullRequestStatus.Draft ||
                pullRequest.Status == GitHubPullRequestStatus.WaitingForReview ||
                pullRequest.Status == GitHubPullRequestStatus.ReadyToMerge),
            GitHubPullRequestFilter.Changes => scoped.Where(pullRequest =>
                pullRequest.Status == GitHubPullRequestStatus.ChangesRequested),
            GitHubPullRequestFilter.Merged => scoped.Where(pullRequest =>
                pullRequest.Status == GitHubPullRequestStatus.Merged),
            _ => scoped,
        };

        var pullRequests = await filtered
            .Include(pullRequest => pullRequest.TaskLinks)
                .ThenInclude(link => link.Task)
                    .ThenInclude(task => task.ReviewerMember)
                        .ThenInclude(member => member!.User)
            .OrderByDescending(pullRequest => pullRequest.LastActivityAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var items = pullRequests.Select(ToItem).ToArray();
        return new GitHubPullRequestFeed(
            items,
            counts?.Waiting ?? 0,
            counts?.Changes ?? 0,
            counts?.Merged ?? 0);
    }

    private static GitHubPullRequestItem ToItem(GitHubPullRequest pullRequest) =>
        new(
            pullRequest.Id,
            pullRequest.RepositoryFullName,
            pullRequest.Number,
            pullRequest.Title,
            pullRequest.Url,
            pullRequest.AuthorLogin,
            SplitReviewers(pullRequest.RequestedReviewers),
            pullRequest.Status,
            pullRequest.CheckStatus,
            pullRequest.OpenedAtUtc,
            pullRequest.LastActivityAtUtc,
            pullRequest.TaskLinks
                .OrderBy(link => link.Task.Code.Value)
                .Select(link => new GitHubPullRequestTaskItem(
                    link.Task.Id,
                    link.Task.Code.Value,
                    link.Task.Title,
                    link.Task.ReviewerMember is null
                        ? null
                        : new GitHubPullRequestReviewer(
                            link.Task.ReviewerMember.UserId,
                            link.Task.ReviewerMember.User.DisplayName,
                            link.Task.ReviewerMember.User.Email)))
                .ToArray());

    private static IReadOnlyList<string> SplitReviewers(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
