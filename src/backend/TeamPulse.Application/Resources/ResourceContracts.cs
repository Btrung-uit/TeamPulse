using TeamPulse.Domain.Resources;

namespace TeamPulse.Application.Resources;

public sealed record SaveResourceCommand(
    string Title,
    ResourceType Type,
    string Url,
    string? ExternalId,
    Guid OwnerUserId,
    Guid? LinkedTaskId);

public sealed record ResourceOwnerSummary(Guid UserId, string DisplayName, string Email);

public sealed record ResourceTaskSummary(Guid Id, string Code, string Title);

public sealed record ResourceSummary(
    Guid Id,
    Guid WorkspaceId,
    Guid ProjectId,
    string Title,
    ResourceType Type,
    string Url,
    string? ExternalId,
    ResourceOwnerSummary Owner,
    ResourceTaskSummary? LinkedTask,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public enum ResourceFailure
{
    None,
    ProjectNotFound,
    ProjectArchived,
    ResourceNotFound,
    OwnerNotFound,
    LinkedTaskNotFound,
}

public sealed record ResourceResult<T>(T? Value, ResourceFailure Failure)
{
    public bool Succeeded => Failure == ResourceFailure.None;
    public static ResourceResult<T> Success(T value) => new(value, ResourceFailure.None);
    public static ResourceResult<T> Failed(ResourceFailure failure) => new(default, failure);
}
