using TeamPulse.Api.Contracts;
using TeamPulse.Application.Search;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class SearchEndpoints
{
    private const int MinimumQueryLength = 2;
    private const int MaximumQueryLength = 100;
    private const int ResultsPerType = 6;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/workspaces/{workspaceId:guid}/search", SearchAsync)
            .WithTags("Search")
            .RequireAuthorization(WorkspacePolicies.Member);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        Guid workspaceId,
        string? query,
        IWorkspaceSearchService searchService,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length is < MinimumQueryLength or > MaximumQueryLength)
        {
            return Results.BadRequest(new ApiErrorResponse(
                "validation_failed",
                $"Search query must contain {MinimumQueryLength} to {MaximumQueryLength} characters."));
        }

        var results = await searchService.SearchAsync(
            workspaceId,
            normalizedQuery,
            ResultsPerType,
            cancellationToken);
        return Results.Ok(new
        {
            results.Projects,
            Tasks = results.Tasks.Select(task => new
            {
                task.Id,
                task.ProjectId,
                task.ProjectName,
                task.Code,
                task.Title,
                Status = task.Status.ToString(),
            }),
            Members = results.Members.Select(member => new
            {
                member.UserId,
                member.DisplayName,
                member.Email,
                Role = member.Role.ToString(),
            }),
        });
    }
}
