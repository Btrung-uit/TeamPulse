using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Workspaces;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces")
            .WithTags("Workspaces")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{workspaceId:guid}", GetAsync)
            .RequireAuthorization(WorkspacePolicies.Member);
        group.MapGet("/{workspaceId:guid}/members", ListMembersAsync)
            .RequireAuthorization(WorkspacePolicies.Member);
        group.MapPost("/{workspaceId:guid}/members", AddMemberAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPatch("/{workspaceId:guid}/members/{userId:guid}/role", ChangeRoleAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        IWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var workspaces = await workspaceService.ListForUserAsync(
            GetRequiredUserId(principal),
            cancellationToken);

        return Results.Ok(workspaces.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkspaceRequest request,
        ClaimsPrincipal principal,
        IWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > Workspace.NameMaximumLength)
        {
            return ValidationError(
                "name",
                $"Workspace name must contain 1 to {Workspace.NameMaximumLength} characters.");
        }

        var workspace = await workspaceService.CreateAsync(
            GetRequiredUserId(principal),
            name,
            cancellationToken);

        return Results.Created($"/api/workspaces/{workspace.Id}", ToResponse(workspace));
    }

    private static async Task<IResult> GetAsync(
        Guid workspaceId,
        ClaimsPrincipal principal,
        IWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceService.GetAsync(
            workspaceId,
            GetRequiredUserId(principal),
            cancellationToken);

        return workspace is null ? Results.NotFound() : Results.Ok(ToResponse(workspace));
    }

    private static async Task<IResult> ListMembersAsync(
        Guid workspaceId,
        IWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var members = await workspaceService.ListMembersAsync(workspaceId, cancellationToken);
        return Results.Ok(members.Select(ToResponse));
    }

    private static async Task<IResult> AddMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        ClaimsPrincipal principal,
        IWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0 ||
            email.Length > 320 ||
            !new EmailAddressAttribute().IsValid(email))
        {
            return ValidationError("email", "Enter a valid registered email address.");
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return ValidationError("role", "Role must be Leader, Member, or Advisor.");
        }

        var result = await workspaceService.AddMemberAsync(
            workspaceId,
            GetRequiredUserId(principal),
            email,
            role,
            cancellationToken);

        if (!result.Succeeded)
        {
            return MapFailure(result.Failure);
        }

        return Results.Created(
            $"/api/workspaces/{workspaceId}/members/{result.Value!.UserId}",
            ToResponse(result.Value));
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid workspaceId,
        Guid userId,
        ChangeWorkspaceRoleRequest request,
        ClaimsPrincipal principal,
        IWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var role))
        {
            return ValidationError("role", "Role must be Leader, Member, or Advisor.");
        }

        var result = await workspaceService.ChangeMemberRoleAsync(
            workspaceId,
            GetRequiredUserId(principal),
            userId,
            role,
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    }

    private static bool TryParseRole(string? value, out WorkspaceRole role)
    {
        var normalized = value?.Trim();
        var isNamedRole = Enum.GetNames<WorkspaceRole>().Any(name =>
            string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase));

        role = default;
        return isNamedRole && Enum.TryParse(normalized, ignoreCase: true, out role);
    }

    private static IResult MapFailure(WorkspaceFailure failure) => failure switch
    {
        WorkspaceFailure.UserNotFound => Results.NotFound(new ApiErrorResponse(
            "user_not_found",
            "No registered user was found for that email address.")),
        WorkspaceFailure.AlreadyMember => Results.Conflict(new ApiErrorResponse(
            "already_member",
            "That user is already an active workspace member.")),
        WorkspaceFailure.LastLeader => Results.Conflict(new ApiErrorResponse(
            "last_leader",
            "Assign another leader before changing the final leader's role.")),
        _ => Results.NotFound(new ApiErrorResponse(
            "member_not_found",
            "The workspace member was not found.")),
    };

    private static IResult ValidationError(string field, string message)
    {
        return Results.BadRequest(new ApiErrorResponse(
            "validation_failed",
            "Please correct the highlighted fields.",
            new Dictionary<string, string[]> { [field] = [message] }));
    }

    private static WorkspaceResponse ToResponse(WorkspaceSummary workspace) =>
        new(workspace.Id, workspace.Name, workspace.Role.ToString(), workspace.MemberCount);

    private static WorkspaceMemberResponse ToResponse(WorkspaceMemberDetails member) =>
        new(member.UserId, member.Email, member.DisplayName, member.Role.ToString());

    public sealed record CreateWorkspaceRequest(string Name);

    public sealed record AddWorkspaceMemberRequest(string Email, string Role);

    public sealed record ChangeWorkspaceRoleRequest(string Role);

    private sealed record WorkspaceResponse(Guid Id, string Name, string Role, int MemberCount);

    private sealed record WorkspaceMemberResponse(
        Guid UserId,
        string Email,
        string DisplayName,
        string Role);
}
