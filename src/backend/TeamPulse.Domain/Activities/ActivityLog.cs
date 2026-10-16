using TeamPulse.Domain.Users;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Activities;

public sealed class ActivityLog
{
    public const int EntityTypeMaximumLength = 80;
    public const int ValueMaximumLength = 160;

    private ActivityLog()
    {
    }

    public ActivityLog(
        Guid id,
        Guid workspaceId,
        Guid actorUserId,
        ActivityAction action,
        string entityType,
        Guid entityId,
        DateTimeOffset occurredAtUtc,
        Guid? subjectUserId = null,
        string? previousValue = null,
        string? newValue = null)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || actorUserId == Guid.Empty ||
            entityId == Guid.Empty)
        {
            throw new ArgumentException("Activity identifiers cannot be empty.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown activity action.");
        }

        Id = id;
        WorkspaceId = workspaceId;
        ActorUserId = actorUserId;
        SubjectUserId = subjectUserId;
        Action = action;
        EntityType = Required(entityType, nameof(entityType), EntityTypeMaximumLength);
        EntityId = entityId;
        OccurredAtUtc = occurredAtUtc;
        PreviousValue = Optional(previousValue, nameof(previousValue), ValueMaximumLength);
        NewValue = Optional(newValue, nameof(newValue), ValueMaximumLength);
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid? SubjectUserId { get; private set; }

    public ActivityAction Action { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string? PreviousValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Workspace Workspace { get; private set; } = null!;

    public User ActorUser { get; private set; } = null!;

    public User? SubjectUser { get; private set; }

    private static string Required(string? value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value is required and cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? Optional(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, parameterName, maximumLength);
    }
}
