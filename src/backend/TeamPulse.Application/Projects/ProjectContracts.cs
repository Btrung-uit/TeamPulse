namespace TeamPulse.Application.Projects;

public sealed record ProjectSummary(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public enum ProjectFailure
{
    None,
    ProjectNotFound,
    ProjectArchived,
}

public sealed record ProjectResult<T>(T? Value, ProjectFailure Failure)
{
    public bool Succeeded => Failure == ProjectFailure.None;

    public static ProjectResult<T> Success(T value) =>
        new(value, ProjectFailure.None);

    public static ProjectResult<T> Failed(ProjectFailure failure) =>
        new(default, failure);
}
