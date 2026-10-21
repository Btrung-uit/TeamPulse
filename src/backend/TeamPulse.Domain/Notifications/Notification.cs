using TeamPulse.Domain.Common;

namespace TeamPulse.Domain.Notifications;

public sealed class Notification : AuditableEntity
{
    public const int TitleMaximumLength = 240;
    public const int MessageMaximumLength = 500;

    private Notification()
    {
    }

    public Notification(
        Guid id,
        Guid workspaceId,
        Guid recipientUserId,
        NotificationKind kind,
        string title,
        string message,
        Guid projectId,
        Guid? taskId = null)
        : base(id)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Notification workspace is required.", nameof(workspaceId));
        }

        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Notification recipient is required.", nameof(recipientUserId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Notification project is required.", nameof(projectId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown notification kind.");
        }

        WorkspaceId = workspaceId;
        RecipientUserId = recipientUserId;
        Kind = kind;
        Title = DomainText.Required(title, nameof(title), TitleMaximumLength);
        Message = DomainText.Required(message, nameof(message), MessageMaximumLength);
        ProjectId = projectId;
        TaskId = taskId;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public NotificationKind Kind { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public Guid ProjectId { get; private set; }

    public Guid? TaskId { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc.HasValue;

    public void MarkRead(DateTimeOffset readAtUtc)
    {
        ReadAtUtc ??= readAtUtc;
    }
}
