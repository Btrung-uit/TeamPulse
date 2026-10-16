using TeamPulse.Domain.Common;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Users;

public sealed class User : AuditableEntity
{
    public const int EmailMaximumLength = 320;
    public const int DisplayNameMaximumLength = 120;

    private User()
    {
    }

    public User(Guid id, string email, string displayName)
        : base(id)
    {
        SetEmail(email);
        Rename(displayName);
    }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public ICollection<Workspace> CreatedWorkspaces { get; } = [];

    public ICollection<WorkspaceMember> WorkspaceMemberships { get; } = [];

    public void SetEmail(string email)
    {
        Email = DomainText.Required(email, nameof(email), EmailMaximumLength);
        NormalizedEmail = Email.ToUpperInvariant();
    }

    public void Rename(string displayName)
    {
        DisplayName = DomainText.Required(
            displayName,
            nameof(displayName),
            DisplayNameMaximumLength);
    }
}
