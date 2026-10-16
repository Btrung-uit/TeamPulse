using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Projects;
using TeamPulse.Domain.Projects;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces/{workspaceId:guid}/projects")
            .WithTags("Projects")
            .RequireAuthorization(WorkspacePolicies.Member);

        group.MapGet("/", ListAsync);
        group.MapGet("/{projectId:guid}", GetAsync);
        group.MapPost("/", CreateAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPut("/{projectId:guid}", UpdateAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPost("/{projectId:guid}/archive", ArchiveAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid workspaceId,
        bool? includeArchived,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var projects = await projectService.ListAsync(
            workspaceId,
            includeArchived ?? false,
            cancellationToken);
        return Results.Ok(projects.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        Guid workspaceId,
        Guid projectId,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var project = await projectService.GetAsync(workspaceId, projectId, cancellationToken);
        return project is null
            ? Results.NotFound(new ApiErrorResponse("project_not_found", "Project was not found."))
            : Results.Ok(ToResponse(project));
    }

    private static async Task<IResult> CreateAsync(
        Guid workspaceId,
        SaveProjectRequest request,
        ClaimsPrincipal principal,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var project = await projectService.CreateAsync(
            workspaceId,
            GetRequiredUserId(principal),
            request.Name!.Trim(),
            NormalizeDescription(request.Description),
            cancellationToken);

        return Results.Created(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}",
            ToResponse(project));
    }

    private static async Task<IResult> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveProjectRequest request,
        ClaimsPrincipal principal,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var result = await projectService.UpdateAsync(
            workspaceId,
            projectId,
            GetRequiredUserId(principal),
            request.Name!.Trim(),
            NormalizeDescription(request.Description),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> ArchiveAsync(
        Guid workspaceId,
        Guid projectId,
        ClaimsPrincipal principal,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var result = await projectService.ArchiveAsync(
            workspaceId,
            projectId,
            GetRequiredUserId(principal),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static IResult? Validate(SaveProjectRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        var description = request.Description?.Trim();

        if (name.Length == 0 || name.Length > Project.NameMaximumLength)
        {
            errors["name"] =
                [$"Project name must contain 1 to {Project.NameMaximumLength} characters."];
        }

        if (description?.Length > Project.DescriptionMaximumLength)
        {
            errors["description"] =
                [$"Description cannot exceed {Project.DescriptionMaximumLength} characters."];
        }

        return errors.Count == 0
            ? null
            : Results.BadRequest(new ApiErrorResponse(
                "validation_failed",
                "Please correct the highlighted fields.",
                errors));
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static Guid GetRequiredUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static IResult MapFailure(ProjectFailure failure) => failure switch
    {
        ProjectFailure.ProjectArchived => Results.Conflict(new ApiErrorResponse(
            "project_archived",
            "Archived projects cannot be changed.")),
        _ => Results.NotFound(new ApiErrorResponse(
            "project_not_found",
            "Project was not found.")),
    };

    private static ProjectResponse ToResponse(ProjectSummary project) =>
        new(
            project.Id,
            project.WorkspaceId,
            project.Name,
            project.Description,
            project.IsArchived,
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            project.ArchivedAtUtc);

    public sealed record SaveProjectRequest(string? Name, string? Description);

    private sealed record ProjectResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        string? Description,
        bool IsArchived,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset? ArchivedAtUtc);
}
