using TeamPulse.Domain.Common;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Tasks;

public sealed class TaskEvidence : AuditableEntity
{
    public const int UrlMaximumLength = 2_048;
    public const int NoteMaximumLength = 500;
    public const int ExternalIdMaximumLength = 256;

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
        : this(
            id,
            workspaceId,
            projectId,
            taskId,
            submittedByUserId,
            TaskEvidenceSource.Manual,
            type,
            url,
            note,
            externalId: null)
    {
    }

    private TaskEvidence(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid? submittedByUserId,
        TaskEvidenceSource source,
        TaskEvidenceType type,
        string url,
        string? note,
        string? externalId)
        : base(id)
    {
        EnsureId(workspaceId, nameof(workspaceId));
        EnsureId(projectId, nameof(projectId));
        EnsureId(taskId, nameof(taskId));
        if (!Enum.IsDefined(source) ||
            (source == TaskEvidenceSource.Manual &&
                (!submittedByUserId.HasValue || submittedByUserId == Guid.Empty)) ||
            (source != TaskEvidenceSource.Manual && submittedByUserId.HasValue))
        {
            throw new ArgumentException("Evidence source and submitter are inconsistent.");
        }

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
        Source = source;
        Type = type;
        Url = normalizedUrl;
        Note = DomainText.Optional(note, nameof(note), NoteMaximumLength);
        ExternalId = source == TaskEvidenceSource.GitHubAutomation
            ? DomainText.Required(externalId, nameof(externalId), ExternalIdMaximumLength)
            : DomainText.Optional(externalId, nameof(externalId), ExternalIdMaximumLength);
    }

    public static TaskEvidence CreateGitHubAutomation(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        TaskEvidenceType type,
        string url,
        string? note,
        string externalId) =>
        new(
            id,
            workspaceId,
            projectId,
            taskId,
            submittedByUserId: null,
            TaskEvidenceSource.GitHubAutomation,
            type,
            url,
            note,
            externalId);

    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public TaskEvidenceSource Source { get; private set; }
    public TaskEvidenceType Type { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public string? ExternalId { get; private set; }
    public ProjectTask Task { get; private set; } = null!;
    public WorkspaceMember? SubmittedByMember { get; private set; }

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
