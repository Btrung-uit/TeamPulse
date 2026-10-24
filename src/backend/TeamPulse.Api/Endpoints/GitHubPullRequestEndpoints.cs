using TeamPulse.Application.Integrations;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class GitHubPullRequestEndpoints
{
    public static IEndpointRouteBuilder MapGitHubPullRequestEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/integrations/github/pull-requests",
                ListAsync)
            .WithTags("GitHub")
            .RequireAuthorization(WorkspacePolicies.Member);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid workspaceId,
        Guid projectId,
        string? filter,
        IGitHubPullRequestService service,
        CancellationToken cancellationToken)
    {
        if (!TryParseFilter(filter, out var parsedFilter))
        {
            return Results.BadRequest(new
            {
                code = "validation_failed",
                message = "Pull request filter must be all, waiting, changes, or merged.",
            });
        }

        var feed = await service.ListAsync(
            workspaceId,
            projectId,
            parsedFilter,
            cancellationToken);
        return Results.Ok(new
        {
            Items = feed.Items.Select(item => new
            {
                item.Id,
                item.RepositoryFullName,
                item.Number,
                item.Title,
                item.Url,
                item.AuthorLogin,
                item.RequestedReviewers,
                Status = item.Status.ToString(),
                CheckStatus = item.CheckStatus.ToString(),
                item.OpenedAtUtc,
                item.LastActivityAtUtc,
                item.Tasks,
            }),
            feed.WaitingCount,
            feed.ChangesCount,
            feed.MergedCount,
        });
    }

    private static bool TryParseFilter(string? value, out GitHubPullRequestFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            filter = GitHubPullRequestFilter.All;
            return true;
        }

        return Enum.TryParse(value, true, out filter) && Enum.IsDefined(filter);
    }
}
