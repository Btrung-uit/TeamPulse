using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Resources;
using TeamPulse.Domain.Resources;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static partial class ResourceEndpoints
{
    public static IEndpointRouteBuilder MapResourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/resources")
            .WithTags("Resources")
            .RequireAuthorization(WorkspacePolicies.Member);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPut("/{resourceId:guid}", UpdateAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapDelete("/{resourceId:guid}", DeleteAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid workspaceId,
        Guid projectId,
        string? search,
        string? type,
        IResourceService service,
        CancellationToken cancellationToken)
    {
        ResourceType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<ResourceType>(type, true, out var value) || !Enum.IsDefined(value))
            {
                return ValidationError("type", "Choose a supported resource type.");
            }

            parsedType = value;
        }

        var result = await service.ListAsync(
            workspaceId,
            projectId,
            search,
            parsedType,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value!.Select(ToResponse))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveResourceRequest request,
        ClaimsPrincipal principal,
        IResourceService service,
        CancellationToken cancellationToken)
    {
        var command = ValidateAndMap(request, out var validation);
        if (validation is not null)
        {
            return validation;
        }

        var result = await service.CreateAsync(
            workspaceId,
            projectId,
            GetRequiredUserId(principal),
            command!,
            cancellationToken);
        return result.Succeeded
            ? Results.Created(
                $"/api/workspaces/{workspaceId}/projects/{projectId}/resources/{result.Value!.Id}",
                ToResponse(result.Value))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        SaveResourceRequest request,
        ClaimsPrincipal principal,
        IResourceService service,
        CancellationToken cancellationToken)
    {
        var command = ValidateAndMap(request, out var validation);
        if (validation is not null)
        {
            return validation;
        }

        var result = await service.UpdateAsync(
            workspaceId,
            projectId,
            resourceId,
            GetRequiredUserId(principal),
            command!,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        ClaimsPrincipal principal,
        IResourceService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(
            workspaceId,
            projectId,
            resourceId,
            GetRequiredUserId(principal),
            cancellationToken);
        return result.Succeeded ? Results.NoContent() : MapFailure(result.Failure);
    }

    private static SaveResourceCommand? ValidateAndMap(
        SaveResourceRequest request,
        out IResult? validation)
    {
        var errors = new Dictionary<string, string[]>();
        var title = request.Title?.Trim();
        var url = request.Url?.Trim();
        var externalId = string.IsNullOrWhiteSpace(request.ExternalId)
            ? null
            : request.ExternalId.Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Length > ProjectResource.TitleMaximumLength)
        {
            errors["title"] = [$"Title is required and cannot exceed {ProjectResource.TitleMaximumLength} characters."];
        }

        if (!Enum.TryParse<ResourceType>(request.Type, true, out var type) || !Enum.IsDefined(type))
        {
            errors["type"] = ["Choose a supported resource type."];
        }

        if (string.IsNullOrWhiteSpace(url) || url.Length > ProjectResource.UrlMaximumLength ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors["url"] = ["Enter an absolute HTTP or HTTPS URL."];
        }

        if (externalId?.Length > ProjectResource.ExternalIdMaximumLength)
        {
            errors["externalId"] = [$"External ID cannot exceed {ProjectResource.ExternalIdMaximumLength} characters."];
        }
        else if (type == ResourceType.GitHub &&
                 (externalId is null || !GitHubRepositoryNamePattern().IsMatch(externalId)))
        {
            errors["externalId"] =
                ["GitHub resources require an External ID in owner/repository format."];
        }

        if (!request.OwnerUserId.HasValue || request.OwnerUserId == Guid.Empty)
        {
            errors["ownerUserId"] = ["Choose a resource owner."];
        }

        if (request.LinkedTaskId == Guid.Empty)
        {
            errors["linkedTaskId"] = ["Linked task ID must be null or non-empty."];
        }

        if (errors.Count > 0)
        {
            validation = Results.BadRequest(new ApiErrorResponse(
                "validation_failed",
                "Please correct the highlighted fields.",
                errors));
            return null;
        }

        validation = null;
        return new SaveResourceCommand(
            title!,
            type,
            url!,
            externalId,
            request.OwnerUserId!.Value,
            request.LinkedTaskId);
    }

    private static IResult MapFailure(ResourceFailure failure) => failure switch
    {
        ResourceFailure.ProjectArchived => Results.Conflict(new ApiErrorResponse(
            "project_archived",
            "Archived projects cannot be changed.")),
        ResourceFailure.OwnerNotFound => Results.BadRequest(new ApiErrorResponse(
            "owner_not_found",
            "Owner must be an active workspace member.")),
        ResourceFailure.LinkedTaskNotFound => Results.BadRequest(new ApiErrorResponse(
            "linked_task_not_found",
            "Linked task was not found in this project.")),
        ResourceFailure.ResourceNotFound => Results.NotFound(new ApiErrorResponse(
            "resource_not_found",
            "Resource was not found in this project.")),
        _ => Results.NotFound(new ApiErrorResponse(
            "project_not_found",
            "Project was not found.")),
    };

    private static ResourceResponse ToResponse(ResourceSummary resource) =>
        new(
            resource.Id,
            resource.WorkspaceId,
            resource.ProjectId,
            resource.Title,
            resource.Type.ToString(),
            resource.Url,
            resource.ExternalId,
            resource.Owner,
            resource.LinkedTask,
            resource.CreatedAtUtc,
            resource.UpdatedAtUtc);

    private static Guid GetRequiredUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public sealed record SaveResourceRequest(
        string? Title,
        string? Type,
        string? Url,
        string? ExternalId,
        Guid? OwnerUserId,
        Guid? LinkedTaskId);

    private sealed record ResourceResponse(
        Guid Id,
        Guid WorkspaceId,
        Guid ProjectId,
        string Title,
        string Type,
        string Url,
        string? ExternalId,
        ResourceOwnerSummary Owner,
        ResourceTaskSummary? LinkedTask,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    private static IResult ValidationError(string field, string message) =>
        Results.BadRequest(new ApiErrorResponse(
            "validation_failed",
            "Please correct the highlighted fields.",
            new Dictionary<string, string[]> { [field] = [message] }));

    [GeneratedRegex(
        "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GitHubRepositoryNamePattern();
}
