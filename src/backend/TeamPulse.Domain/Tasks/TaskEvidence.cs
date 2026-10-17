using TeamPulse.Domain.Common;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Tasks;

public sealed class TaskEvidence : AuditableEntity
{
    public const int UrlMaximumLength = 2_048;
    public const int NoteMaximumLength = 500;

    private TaskEvidence()
    {
    }

    public TaskEvidence(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid submittedByUserId,
        TaskEvidenceType type,
        string url,
        string? note = null)
        : base(id)
    {
        EnsureId(workspaceId, nameof(workspaceId));
        EnsureId(projectId, nameof(projectId));
        EnsureId(taskId, nameof(taskId));
        EnsureId(submittedByUserId, nameof(submittedByUserId));

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown evidence type.");
        }

        var normalizedUrl = DomainText.Required(url, nameof(url), UrlMaximumLength);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Evidence URL must be an absolute HTTP or HTTPS URL.",
                nameof(url));
        }

        WorkspaceId = workspaceId;
        ProjectId = projectId;
        TaskId = taskId;
        SubmittedByUserId = submittedByUserId;
        Type = type;
        Url = normalizedUrl;
        Note = DomainText.Optional(note, nameof(note), NoteMaximumLength);
    }

    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public TaskEvidenceType Type { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public ProjectTask Task { get; private set; } = null!;
    public WorkspaceMember SubmittedByMember { get; private set; } = null!;

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
