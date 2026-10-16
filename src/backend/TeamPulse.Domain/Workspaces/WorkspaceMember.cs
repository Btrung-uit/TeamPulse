using TeamPulse.Domain.Common;
using TeamPulse.Domain.Users;

namespace TeamPulse.Domain.Workspaces;

public sealed class WorkspaceMember : IAuditableEntity
{
    private WorkspaceMember()
    {
    }

    public WorkspaceMember(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace is required.", nameof(workspaceId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(userId));
        }

        EnsureValidRole(role);

        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        IsActive = true;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Workspace Workspace { get; private set; } = null!;

    public User User { get; private set; } = null!;

    public void ChangeRole(WorkspaceRole role)
    {
        EnsureValidRole(role);
        Role = role;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    private static void EnsureValidRole(WorkspaceRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown workspace role.");
        }
    }
}
