using TeamPulse.Domain.Resources;

namespace TeamPulse.Application.Resources;

public interface IResourceService
{
    Task<ResourceResult<IReadOnlyList<ResourceSummary>>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        string? search,
        ResourceType? type,
        CancellationToken cancellationToken = default);

    Task<ResourceResult<ResourceSummary>> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        SaveResourceCommand command,
        CancellationToken cancellationToken = default);

    Task<ResourceResult<ResourceSummary>> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        Guid actorUserId,
        SaveResourceCommand command,
        CancellationToken cancellationToken = default);

    Task<ResourceResult<bool>> DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
