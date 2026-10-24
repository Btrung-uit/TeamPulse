using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Application.Notifications;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class NotificationEndpoints
{
    private const int DefaultLimit = 30;
    private const int MaximumLimit = 100;

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/workspaces/{workspaceId:guid}/notifications")
            .WithTags("Notifications")
            .RequireAuthorization(WorkspacePolicies.Member);

        group.MapGet("/", ListAsync);
        group.MapPatch("/{notificationId:guid}/read", MarkReadAsync);
        group.MapPost("/read-all", MarkAllReadAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid workspaceId,
        string? filter,
        int? limit,
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        if (!TryParseFilter(filter, out var parsedFilter))
        {
            return Results.BadRequest(new
            {
                code = "validation_failed",
                message = "Notification filter must be all, unread, or reviews.",
            });
        }

        var resolvedLimit = limit ?? DefaultLimit;
        if (resolvedLimit is < 1 or > MaximumLimit)
        {
            return Results.BadRequest(new
            {
                code = "validation_failed",
                message = $"Notification limit must be between 1 and {MaximumLimit}.",
            });
        }

        var feed = await notificationService.ListAsync(
            workspaceId,
            GetRequiredUserId(principal),
            parsedFilter,
            resolvedLimit,
            cancellationToken);
        return Results.Ok(new
        {
            Items = feed.Items.Select(notification => new
            {
                notification.Id,
                Kind = notification.Kind.ToString(),
                notification.Title,
                notification.Message,
                notification.ProjectId,
                notification.TaskId,
                notification.ExternalUrl,
                notification.IsRead,
                notification.CreatedAtUtc,
            }),
            feed.UnreadCount,
        });
    }

    private static async Task<IResult> MarkReadAsync(
        Guid workspaceId,
        Guid notificationId,
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var found = await notificationService.MarkReadAsync(
            workspaceId,
            GetRequiredUserId(principal),
            notificationId,
            cancellationToken);
        return found ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> MarkAllReadAsync(
        Guid workspaceId,
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var markedCount = await notificationService.MarkAllReadAsync(
            workspaceId,
            GetRequiredUserId(principal),
            cancellationToken);
        return Results.Ok(new { markedCount });
    }

    private static bool TryParseFilter(string? value, out NotificationFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            filter = NotificationFilter.All;
            return true;
        }

        return Enum.TryParse(value, true, out filter) && Enum.IsDefined(filter);
    }

    private static Guid GetRequiredUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
