using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Dashboard;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/dashboard",
                GetAsync)
            .WithTags("Dashboard")
            .RequireAuthorization(WorkspacePolicies.Member);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid workspaceId,
        Guid projectId,
        ClaimsPrincipal principal,
        IDashboardService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(
            workspaceId,
            projectId,
            Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!),
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : Results.NotFound(new ApiErrorResponse(
                "project_not_found",
                "Project was not found."));
    }

    private static DashboardResponse ToResponse(ProjectDashboard dashboard) =>
        new(
            dashboard.Kpis,
            dashboard.StatusBreakdown.Select(item => new StatusCountResponse(
                item.Status.ToString(),
                item.Count)).ToArray(),
            dashboard.Workload.Select(item => new WorkloadResponse(
                item.UserId,
                item.DisplayName,
                item.Role.ToString(),
                item.OpenTasks,
                item.BlockedTasks,
                item.DueSoonTasks)).ToArray(),
            dashboard.MyPriorityTasks.Select(item => new PriorityTaskResponse(
                item.Id,
                item.Code,
                item.Title,
                item.Status.ToString(),
                item.Priority.ToString(),
                item.DueOn)).ToArray(),
            dashboard.RecentActivity.Select(item => new ActivityResponse(
                item.Id,
                item.ActorUserId,
                item.ActorDisplayName,
                item.Action.ToString(),
                item.EntityId,
                item.TaskCode,
                item.PreviousValue,
                item.NewValue,
                item.OccurredAtUtc)).ToArray(),
            dashboard.Contributions.Select(item => new ContributionResponse(
                item.UserId,
                item.DisplayName,
                item.Role.ToString(),
                item.AcceptedTasks,
                item.AcceptedEvidence,
                item.Reviews,
                item.LastContributedAtUtc)).ToArray());

    private sealed record DashboardResponse(
        DashboardKpis Kpis,
        IReadOnlyList<StatusCountResponse> StatusBreakdown,
        IReadOnlyList<WorkloadResponse> Workload,
        IReadOnlyList<PriorityTaskResponse> MyPriorityTasks,
        IReadOnlyList<ActivityResponse> RecentActivity,
        IReadOnlyList<ContributionResponse> Contributions);
    private sealed record StatusCountResponse(string Status, int Count);
    private sealed record WorkloadResponse(
        Guid UserId,
        string DisplayName,
        string Role,
        int OpenTasks,
        int BlockedTasks,
        int DueSoonTasks);
    private sealed record PriorityTaskResponse(
        Guid Id,
        string Code,
        string Title,
        string Status,
        string Priority,
        DateOnly? DueOn);
    private sealed record ActivityResponse(
        Guid Id,
        Guid ActorUserId,
        string ActorDisplayName,
        string Action,
        Guid EntityId,
        string? TaskCode,
        string? PreviousValue,
        string? NewValue,
        DateTimeOffset OccurredAtUtc);
    private sealed record ContributionResponse(
        Guid UserId,
        string DisplayName,
        string Role,
        int AcceptedTasks,
        int AcceptedEvidence,
        int Reviews,
        DateTimeOffset? LastContributedAtUtc);
}
