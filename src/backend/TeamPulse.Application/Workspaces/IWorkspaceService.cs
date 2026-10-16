using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Application.Workspaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceSummary>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSummary> CreateAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSummary?> GetAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceMemberDetails>> ListMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceResult<WorkspaceMemberDetails>> AddMemberAsync(
        Guid workspaceId,
        Guid actorUserId,
        string email,
        WorkspaceRole role,
        CancellationToken cancellationToken = default);

    Task<WorkspaceResult<WorkspaceMemberDetails>> ChangeMemberRoleAsync(
        Guid workspaceId,
        Guid actorUserId,
        Guid memberUserId,
        WorkspaceRole role,
        CancellationToken cancellationToken = default);
}
