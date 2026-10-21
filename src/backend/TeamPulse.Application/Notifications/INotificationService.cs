namespace TeamPulse.Application.Notifications;

public interface INotificationService
{
    Task<NotificationFeed> ListAsync(
        Guid workspaceId,
        Guid recipientUserId,
        NotificationFilter filter,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid workspaceId,
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid workspaceId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);
}
