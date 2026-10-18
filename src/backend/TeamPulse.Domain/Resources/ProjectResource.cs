using TeamPulse.Domain.Common;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Resources;

public sealed class ProjectResource : AuditableEntity
{
    public const int TitleMaximumLength = 200;
    public const int UrlMaximumLength = 2_048;
    public const int ExternalIdMaximumLength = 256;

    private ProjectResource()
    {
    }

    public ProjectResource(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid ownerUserId,
        string title,
        ResourceType type,
        string url,
        string? externalId = null,
        Guid? linkedTaskId = null)
        : base(id)
    {
        EnsureId(workspaceId, nameof(workspaceId));
        EnsureId(projectId, nameof(projectId));
        Update(title, type, url, externalId, ownerUserId, linkedTaskId);
        WorkspaceId = workspaceId;
        ProjectId = projectId;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ResourceType Type { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? ExternalId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? LinkedTaskId { get; private set; }
    public Project Project { get; private set; } = null!;
    public WorkspaceMember OwnerMember { get; private set; } = null!;
    public ProjectTask? LinkedTask { get; private set; }

    public void Update(
        string title,
        ResourceType type,
        string url,
        string? externalId,
        Guid ownerUserId,
        Guid? linkedTaskId)
    {
        EnsureId(ownerUserId, nameof(ownerUserId));
        if (linkedTaskId == Guid.Empty)
        {
            throw new ArgumentException("Linked task ID cannot be empty.", nameof(linkedTaskId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown resource type.");
        }

        var normalizedUrl = DomainText.Required(url, nameof(url), UrlMaximumLength);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Resource URL must be an absolute HTTP or HTTPS URL.",
                nameof(url));
        }

        Title = DomainText.Required(title, nameof(title), TitleMaximumLength);
        Type = type;
        Url = normalizedUrl;
        ExternalId = DomainText.Optional(externalId, nameof(externalId), ExternalIdMaximumLength);
        OwnerUserId = ownerUserId;
        LinkedTaskId = linkedTaskId;
    }

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
