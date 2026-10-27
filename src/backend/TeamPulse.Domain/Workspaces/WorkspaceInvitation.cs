using TeamPulse.Domain.Common;
using TeamPulse.Domain.Users;

namespace TeamPulse.Domain.Workspaces;

public sealed class WorkspaceInvitation : AuditableEntity
{
    public const int TokenHashMaximumLength = 64;

    private WorkspaceInvitation()
    {
    }

    public WorkspaceInvitation(
        Guid id,
        Guid workspaceId,
        Guid invitedByUserId,
        string email,
        WorkspaceRole role,
        string tokenHash,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        WorkspaceId = workspaceId;
        InvitedByUserId = invitedByUserId;
        Renew(email, role, tokenHash, expiresAtUtc);
    }

    public Guid WorkspaceId { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public WorkspaceRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public Workspace Workspace { get; private set; } = null!;

    public void Renew(
        string email,
        WorkspaceRole role,
        string tokenHash,
        DateTimeOffset expiresAtUtc)
    {
        Email = DomainText.Required(email, nameof(email), User.EmailMaximumLength);
        NormalizedEmail = Email.ToUpperInvariant();
        Role = Enum.IsDefined(role)
            ? role
            : throw new ArgumentOutOfRangeException(nameof(role));
        TokenHash = DomainText.Required(
            tokenHash,
            nameof(tokenHash),
            TokenHashMaximumLength);
        ExpiresAtUtc = expiresAtUtc;
        AcceptedAtUtc = null;
    }

    public bool CanBeAcceptedAt(DateTimeOffset now) =>
        AcceptedAtUtc is null && ExpiresAtUtc > now;

    public void Accept(DateTimeOffset acceptedAtUtc)
    {
        AcceptedAtUtc = acceptedAtUtc;
    }
}
