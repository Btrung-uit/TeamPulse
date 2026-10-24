using TeamPulse.Domain.Notifications;

namespace TeamPulse.Application.Notifications;

public enum NotificationFilter
{
    All,
    Unread,
    Reviews,
}

public sealed record NotificationDetails(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Message,
    Guid ProjectId,
    Guid? TaskId,
    string? ExternalUrl,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationFeed(
    IReadOnlyList<NotificationDetails> Items,
    int UnreadCount);
