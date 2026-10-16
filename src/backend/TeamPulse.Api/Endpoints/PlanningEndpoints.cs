using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Planning;
using TeamPulse.Domain.Projects;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class PlanningEndpoints
{
    public static IEndpointRouteBuilder MapPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/workspaces/{workspaceId:guid}/projects/{projectId:guid}")
            .WithTags("Planning")
            .RequireAuthorization(WorkspacePolicies.Member);

        group.MapGet("/sprints", ListSprintsAsync);
        group.MapPost("/sprints", CreateSprintAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPut("/sprints/{sprintId:guid}", UpdateSprintAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapDelete("/sprints/{sprintId:guid}", DeleteSprintAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        group.MapPut("/tasks/{taskId:guid}/sprint", AssignTaskToSprintAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        group.MapGet("/dependencies", ListDependenciesAsync);
        group.MapPost("/dependencies", CreateDependencyAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapDelete(
                "/dependencies/{predecessorTaskId:guid}/{successorTaskId:guid}",
                DeleteDependencyAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        return endpoints;
    }

    private static async Task<IResult> ListSprintsAsync(
        Guid workspaceId,
        Guid projectId,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var result = await planningService.ListSprintsAsync(
            workspaceId,
            projectId,
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value!.Select(ToResponse))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> CreateSprintAsync(
        Guid workspaceId,
        Guid projectId,
        SaveSprintRequest request,
        ClaimsPrincipal principal,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var result = await planningService.CreateSprintAsync(
            workspaceId,
            projectId,
            GetRequiredUserId(principal),
            ToCommand(request),
            cancellationToken);

        return result.Succeeded
            ? Results.Created(
                $"/api/workspaces/{workspaceId}/projects/{projectId}/sprints/{result.Value!.Id}",
                ToResponse(result.Value))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> UpdateSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        SaveSprintRequest request,
        ClaimsPrincipal principal,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var result = await planningService.UpdateSprintAsync(
            workspaceId,
            projectId,
            sprintId,
            GetRequiredUserId(principal),
            ToCommand(request),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> DeleteSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        ClaimsPrincipal principal,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var result = await planningService.DeleteSprintAsync(
            workspaceId,
            projectId,
            sprintId,
            GetRequiredUserId(principal),
            cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> AssignTaskToSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        AssignTaskToSprintRequest request,
        ClaimsPrincipal principal,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        if (request.SprintId == Guid.Empty)
        {
            return ValidationError(
                "sprintId",
                "Sprint ID must be null to unassign or a non-empty identifier.");
        }

        var result = await planningService.AssignTaskToSprintAsync(
            workspaceId,
            projectId,
            taskId,
            request.SprintId,
            GetRequiredUserId(principal),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(new TaskSprintAssignmentResponse(
                result.Value!.TaskId,
                result.Value.TaskCode,
                result.Value.SprintId,
                result.Value.SprintName))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> ListDependenciesAsync(
        Guid workspaceId,
        Guid projectId,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var result = await planningService.ListDependenciesAsync(
            workspaceId,
            projectId,
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value!.Select(ToResponse))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> CreateDependencyAsync(
        Guid workspaceId,
        Guid projectId,
        SaveDependencyRequest request,
        ClaimsPrincipal principal,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        if (request.PredecessorTaskId == Guid.Empty || request.SuccessorTaskId == Guid.Empty)
        {
            return Results.BadRequest(new ApiErrorResponse(
                "validation_failed",
                "Please correct the highlighted fields.",
                new Dictionary<string, string[]>
                {
                    ["dependency"] = ["Both task identifiers are required."],
                }));
        }

        var result = await planningService.CreateDependencyAsync(
            workspaceId,
            projectId,
            request.PredecessorTaskId,
            request.SuccessorTaskId,
            GetRequiredUserId(principal),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> DeleteDependencyAsync(
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId,
        ClaimsPrincipal principal,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var result = await planningService.DeleteDependencyAsync(
            workspaceId,
            projectId,
            predecessorTaskId,
            successorTaskId,
            GetRequiredUserId(principal),
            cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : MapFailure(result.Failure);
    }

    private static IResult? Validate(SaveSprintRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        var goal = request.Goal?.Trim();

        if (name.Length == 0 || name.Length > Sprint.NameMaximumLength)
        {
            errors["name"] =
                [$"Sprint name must contain 1 to {Sprint.NameMaximumLength} characters."];
        }

        if (goal?.Length > Sprint.GoalMaximumLength)
        {
            errors["goal"] = [$"Goal cannot exceed {Sprint.GoalMaximumLength} characters."];
        }

        if (!request.StartsOn.HasValue)
        {
            errors["startsOn"] = ["Sprint start date is required."];
        }

        if (!request.EndsOn.HasValue)
        {
            errors["endsOn"] = ["Sprint end date is required."];
        }
        else if (request.StartsOn.HasValue && request.EndsOn < request.StartsOn)
        {
            errors["endsOn"] = ["Sprint end date cannot be before its start date."];
        }

        return errors.Count == 0
            ? null
            : Results.BadRequest(new ApiErrorResponse(
                "validation_failed",
                "Please correct the highlighted fields.",
                errors));
    }

    private static IResult ValidationError(string field, string message) =>
        Results.BadRequest(new ApiErrorResponse(
            "validation_failed",
            "Please correct the highlighted fields.",
            new Dictionary<string, string[]> { [field] = [message] }));

    private static SaveSprintCommand ToCommand(SaveSprintRequest request) =>
        new(
            request.Name!.Trim(),
            string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim(),
            request.StartsOn!.Value,
            request.EndsOn!.Value);

    private static IResult MapFailure(PlanningFailure failure) => failure switch
    {
        PlanningFailure.ProjectArchived => Results.Conflict(new ApiErrorResponse(
            "project_archived",
            "Archived projects cannot be changed.")),
        PlanningFailure.SprintNameConflict => Results.Conflict(new ApiErrorResponse(
            "sprint_name_conflict",
            "A sprint with this name already exists in the project.")),
        PlanningFailure.SprintHasTasks => Results.Conflict(new ApiErrorResponse(
            "sprint_has_tasks",
            "Unassign its tasks before deleting this sprint.")),
        PlanningFailure.TaskArchived => Results.Conflict(new ApiErrorResponse(
            "task_archived",
            "Archived tasks cannot be changed.")),
        PlanningFailure.SelfDependency => Results.Conflict(new ApiErrorResponse(
            "self_dependency",
            "A task cannot depend on itself.")),
        PlanningFailure.DependencyCycle => Results.Conflict(new ApiErrorResponse(
            "dependency_cycle",
            "This dependency would create a cycle.")),
        PlanningFailure.SprintNotFound => Results.NotFound(new ApiErrorResponse(
            "sprint_not_found",
            "Sprint was not found in this project.")),
        PlanningFailure.TaskNotFound => Results.NotFound(new ApiErrorResponse(
            "task_not_found",
            "Task was not found in this project.")),
        _ => Results.NotFound(new ApiErrorResponse(
            "project_not_found",
            "Project was not found.")),
    };

    private static SprintResponse ToResponse(SprintSummary sprint) =>
        new(
            sprint.Id,
            sprint.WorkspaceId,
            sprint.ProjectId,
            sprint.Name,
            sprint.Goal,
            sprint.StartsOn,
            sprint.EndsOn,
            sprint.TaskCount,
            sprint.CreatedAtUtc,
            sprint.UpdatedAtUtc);

    private static TaskDependencyResponse ToResponse(TaskDependencySummary dependency) =>
        new(
            ToResponse(dependency.Predecessor),
            ToResponse(dependency.Successor),
            dependency.IsAtRisk,
            dependency.Risks.Select(risk => new DependencyRiskResponse(
                risk.Code,
                risk.Message)).ToArray(),
            dependency.CreatedAtUtc);

    private static DependencyTaskResponse ToResponse(DependencyTaskSummary task) =>
        new(
            task.Id,
            task.Code,
            task.Title,
            task.Status.ToString(),
            task.DueOn,
            task.IsArchived);

    private static Guid GetRequiredUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public sealed record SaveSprintRequest(
        string? Name,
        string? Goal,
        DateOnly? StartsOn,
        DateOnly? EndsOn);

    public sealed record AssignTaskToSprintRequest(Guid? SprintId);

    public sealed record SaveDependencyRequest(
        Guid PredecessorTaskId,
        Guid SuccessorTaskId);

    private sealed record SprintResponse(
        Guid Id,
        Guid WorkspaceId,
        Guid ProjectId,
        string Name,
        string? Goal,
        DateOnly StartsOn,
        DateOnly EndsOn,
        int TaskCount,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    private sealed record TaskSprintAssignmentResponse(
        Guid TaskId,
        string TaskCode,
        Guid? SprintId,
        string? SprintName);

    private sealed record DependencyTaskResponse(
        Guid Id,
        string Code,
        string Title,
        string Status,
        DateOnly? DueOn,
        bool IsArchived);

    private sealed record DependencyRiskResponse(string Code, string Message);

    private sealed record TaskDependencyResponse(
        DependencyTaskResponse Predecessor,
        DependencyTaskResponse Successor,
        bool IsAtRisk,
        IReadOnlyList<DependencyRiskResponse> Risks,
        DateTimeOffset CreatedAtUtc);
}
