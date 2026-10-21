namespace TeamPulse.Application.Search;

public interface IWorkspaceSearchService
{
    Task<WorkspaceSearchResults> SearchAsync(
        Guid workspaceId,
        string query,
        int limitPerType,
        CancellationToken cancellationToken = default);
}
