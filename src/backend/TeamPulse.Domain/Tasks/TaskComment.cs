using TeamPulse.Domain.Common;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Tasks;

public sealed class TaskComment : AuditableEntity
{
    public const int BodyMaximumLength = 2_000;

    private TaskComment()
    {
    }

    public TaskComment(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid authorUserId,
        TaskCommentKind kind,
        string body)
        : base(id)
    {
        EnsureId(workspaceId, nameof(workspaceId));
        EnsureId(projectId, nameof(projectId));
        EnsureId(taskId, nameof(taskId));
        EnsureId(authorUserId, nameof(authorUserId));

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown comment kind.");
        }

        WorkspaceId = workspaceId;
        ProjectId = projectId;
        TaskId = taskId;
        AuthorUserId = authorUserId;
        Kind = kind;
        Body = DomainText.Required(body, nameof(body), BodyMaximumLength);
    }

    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public TaskCommentKind Kind { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public ProjectTask Task { get; private set; } = null!;
    public WorkspaceMember AuthorMember { get; private set; } = null!;

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
