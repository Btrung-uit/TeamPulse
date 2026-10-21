using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Notifications;
using TeamPulse.Domain.Notifications;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Notifications;

internal sealed class NotificationService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : INotificationService
{
    public async Task<NotificationFeed> ListAsync(
        Guid workspaceId,
        Guid recipientUserId,
        NotificationFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var recipientNotifications = dbContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.WorkspaceId == workspaceId &&
                notification.RecipientUserId == recipientUserId);

        var unreadCount = await recipientNotifications.CountAsync(
            notification => notification.ReadAtUtc == null,
            cancellationToken);

        var filtered = filter switch
        {
            NotificationFilter.Unread => recipientNotifications.Where(
                notification => notification.ReadAtUtc == null),
            NotificationFilter.Reviews => recipientNotifications.Where(
                notification =>
                    notification.Kind == NotificationKind.ReviewRequest ||
                    notification.Kind == NotificationKind.ReviewAccepted ||
                    notification.Kind == NotificationKind.ChangesRequested),
            _ => recipientNotifications,
        };

        var items = await filtered
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(limit)
            .Select(notification => new NotificationDetails(
                notification.Id,
                notification.Kind,
                notification.Title,
                notification.Message,
                notification.ProjectId,
                notification.TaskId,
                notification.ReadAtUtc != null,
                notification.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new NotificationFeed(items, unreadCount);
    }

    public async Task<bool> MarkReadAsync(
        Guid workspaceId,
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.RecipientUserId == recipientUserId &&
                candidate.Id == notificationId,
            cancellationToken);

        if (notification is null)
        {
            return false;
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(
        Guid workspaceId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await dbContext.Notifications
            .Where(notification =>
                notification.WorkspaceId == workspaceId &&
                notification.RecipientUserId == recipientUserId &&
                notification.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        foreach (var notification in notifications)
        {
            notification.MarkRead(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }
}
