using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Application.Search;

public sealed record ProjectSearchResult(
    Guid Id,
    string Name,
    string? Description);

public sealed record TaskSearchResult(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Code,
    string Title,
    ProjectTaskStatus Status);

public sealed record MemberSearchResult(
    Guid UserId,
    string DisplayName,
    string Email,
    WorkspaceRole Role);

public sealed record WorkspaceSearchResults(
    IReadOnlyList<ProjectSearchResult> Projects,
    IReadOnlyList<TaskSearchResult> Tasks,
    IReadOnlyList<MemberSearchResult> Members);
