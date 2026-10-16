using TeamPulse.Domain.Common;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Projects;

public sealed class Project : AuditableEntity
{
    public const int NameMaximumLength = 160;
    public const int DescriptionMaximumLength = 2_000;

    private Project()
    {
    }

    public Project(
        Guid id,
        Guid workspaceId,
        Guid createdByUserId,
        string name,
        string? description = null)
        : base(id)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Project workspace is required.", nameof(workspaceId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Project creator is required.", nameof(createdByUserId));
        }

        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        Name = DomainText.Required(name, nameof(name), NameMaximumLength);
        Description = DomainText.Optional(
            description,
            nameof(description),
            DescriptionMaximumLength);
    }

    public Guid WorkspaceId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public Workspace Workspace { get; private set; } = null!;

    public WorkspaceMember CreatedByMember { get; private set; } = null!;

    public ICollection<Sprint> Sprints { get; } = [];

    public ICollection<ProjectTask> Tasks { get; } = [];

    public void UpdateDetails(string name, string? description)
    {
        Name = DomainText.Required(name, nameof(name), NameMaximumLength);
        Description = DomainText.Optional(
            description,
            nameof(description),
            DescriptionMaximumLength);
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        IsArchived = true;
        ArchivedAtUtc = archivedAtUtc;
    }
}
