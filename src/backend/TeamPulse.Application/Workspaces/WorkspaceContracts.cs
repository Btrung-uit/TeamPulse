using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Application.Workspaces;

public sealed record WorkspaceSummary(
    Guid Id,
    string Name,
    WorkspaceRole Role,
    int MemberCount);

public sealed record WorkspaceMemberDetails(
    Guid UserId,
    string Email,
    string DisplayName,
    WorkspaceRole Role);

public enum WorkspaceFailure
{
    None,
    WorkspaceNotFound,
    UserNotFound,
    AlreadyMember,
    LastLeader,
    InvalidInvitation,
}

public sealed record WorkspaceResult<T>(T? Value, WorkspaceFailure Failure)
{
    public bool Succeeded => Failure == WorkspaceFailure.None;

    public static WorkspaceResult<T> Success(T value) =>
        new(value, WorkspaceFailure.None);

    public static WorkspaceResult<T> Failed(WorkspaceFailure failure) =>
        new(default, failure);
}
