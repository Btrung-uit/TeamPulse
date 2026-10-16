namespace TeamPulse.Application.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectSummary>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<ProjectSummary?> GetAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectSummary> CreateAsync(
        Guid workspaceId,
        Guid actorUserId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ProjectResult<ProjectSummary>> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ProjectResult<ProjectSummary>> ArchiveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
