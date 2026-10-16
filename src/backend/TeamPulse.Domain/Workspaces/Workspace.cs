using TeamPulse.Domain.Common;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Users;

namespace TeamPulse.Domain.Workspaces;

public sealed class Workspace : AuditableEntity
{
    public const int NameMaximumLength = 160;

    private Workspace()
    {
    }

    public Workspace(Guid id, string name, Guid createdByUserId)
        : base(id)
    {
        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Workspace creator is required.", nameof(createdByUserId));
        }

        Name = DomainText.Required(name, nameof(name), NameMaximumLength);
        CreatedByUserId = createdByUserId;
    }

    public string Name { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public User CreatedByUser { get; private set; } = null!;

    public ICollection<WorkspaceMember> Members { get; } = [];

    public ICollection<Project> Projects { get; } = [];

    public void Rename(string name)
    {
        Name = DomainText.Required(name, nameof(name), NameMaximumLength);
    }
}
