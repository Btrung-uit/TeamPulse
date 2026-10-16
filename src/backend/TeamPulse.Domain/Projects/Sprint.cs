using TeamPulse.Domain.Common;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Projects;

public sealed class Sprint : AuditableEntity
{
    public const int NameMaximumLength = 160;
    public const int GoalMaximumLength = 1_000;

    private Sprint()
    {
    }

    public Sprint(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid createdByUserId,
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        string? goal = null)
        : base(id)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Sprint workspace is required.", nameof(workspaceId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Sprint project is required.", nameof(projectId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Sprint creator is required.", nameof(createdByUserId));
        }

        if (endsOn < startsOn)
        {
            throw new ArgumentException("Sprint end date cannot be before its start date.", nameof(endsOn));
        }

        WorkspaceId = workspaceId;
        ProjectId = projectId;
        CreatedByUserId = createdByUserId;
        Name = DomainText.Required(name, nameof(name), NameMaximumLength);
        Goal = DomainText.Optional(goal, nameof(goal), GoalMaximumLength);
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Goal { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    public Project Project { get; private set; } = null!;

    public WorkspaceMember CreatedByMember { get; private set; } = null!;

    public ICollection<ProjectTask> Tasks { get; } = [];
}
