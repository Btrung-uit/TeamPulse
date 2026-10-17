using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class TaskEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/tasks")
            .WithTags("Tasks")
            .RequireAuthorization(WorkspacePolicies.Member);

        group.MapGet("/", ListAsync);
        group.MapGet("/{taskId:guid}", GetAsync);
        group.MapPost("/", CreateAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPut("/{taskId:guid}", UpdateAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPatch("/{taskId:guid}/status", ChangeStatusAsync);
        group.MapPost("/{taskId:guid}/archive", ArchiveAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid workspaceId,
        Guid projectId,
        int? page,
        int? pageSize,
        string? search,
        string? status,
        string? priority,
        Guid? assigneeUserId,
        Guid? sprintId,
        DateOnly? dueBefore,
        bool? includeArchived,
        ITaskService taskService,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? DefaultPageSize;
        var errors = new Dictionary<string, string[]>();
        ProjectTaskStatus? parsedStatus = null;
        TaskPriority? parsedPriority = null;

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be at least 1."];
        }

        if (resolvedPageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaximumPageSize}."];
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (TryParseStatus(status, out var value))
            {
                parsedStatus = value;
            }
            else
            {
                errors["status"] = ["Unknown task status."];
            }
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (TryParsePriority(priority, out var value))
            {
                parsedPriority = value;
            }
            else
            {
                errors["priority"] = ["Unknown task priority."];
            }
        }

        if (assigneeUserId == Guid.Empty)
        {
            errors["assigneeUserId"] = ["Assignee ID cannot be empty."];
        }

        if (sprintId == Guid.Empty)
        {
            errors["sprintId"] = ["Sprint ID cannot be empty."];
        }

        if (errors.Count > 0)
        {
            return ValidationError(errors);
        }

        var result = await taskService.ListAsync(
            workspaceId,
            projectId,
            new TaskListQuery(
                resolvedPage,
                resolvedPageSize,
                NormalizeOptional(search),
                parsedStatus,
                parsedPriority,
                assigneeUserId,
                sprintId,
                dueBefore,
                includeArchived ?? false),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        ITaskService taskService,
        CancellationToken cancellationToken)
    {
        var result = await taskService.GetAsync(
            workspaceId,
            projectId,
            taskId,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveTaskRequest request,
        ClaimsPrincipal principal,
        ITaskService taskService,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSaveRequest(request, requireRowVersion: false);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var result = await taskService.CreateAsync(
            workspaceId,
            projectId,
            GetRequiredUserId(principal),
            new CreateTaskCommand(
                validation.Code,
                request.Title!.Trim(),
                NormalizeOptional(request.Description),
                request.AssigneeUserId,
                request.ReviewerUserId,
                request.SprintId,
                validation.Priority,
                request.DueOn,
                request.EstimateHours),
            cancellationToken);

        return result.Succeeded
            ? Results.Created(
                $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks/{result.Value!.Id}",
                ToResponse(result.Value))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        SaveTaskRequest request,
        ClaimsPrincipal principal,
        ITaskService taskService,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSaveRequest(request, requireRowVersion: true);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var result = await taskService.UpdateAsync(
            workspaceId,
            projectId,
            taskId,
            GetRequiredUserId(principal),
            new UpdateTaskCommand(
                validation.Code,
                request.Title!.Trim(),
                NormalizeOptional(request.Description),
                request.AssigneeUserId,
                request.ReviewerUserId,
                request.SprintId,
                validation.Priority,
                request.DueOn,
                request.EstimateHours,
                validation.RowVersion),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        ChangeStatusRequest request,
        ClaimsPrincipal principal,
        ITaskService taskService,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!TryParseStatus(request.Status, out var status))
        {
            errors["status"] = ["Unknown task status."];
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            errors["rowVersion"] = ["Row version must be a valid Base64 string."];
        }

        if (errors.Count > 0)
        {
            return ValidationError(errors);
        }

        var result = await taskService.ChangeStatusAsync(
            workspaceId,
            projectId,
            taskId,
            GetRequiredUserId(principal),
            new ChangeTaskStatusCommand(status, rowVersion),
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> ArchiveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        ArchiveTaskRequest request,
        ClaimsPrincipal principal,
        ITaskService taskService,
        CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            return ValidationError(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Row version must be a valid Base64 string."],
            });
        }

        var result = await taskService.ArchiveAsync(
            workspaceId,
            projectId,
            taskId,
            GetRequiredUserId(principal),
            rowVersion,
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static SaveTaskValidation ValidateSaveRequest(
        SaveTaskRequest request,
        bool requireRowVersion)
    {
        var errors = new Dictionary<string, string[]>();
        var code = default(TaskCode);
        var priority = default(TaskPriority);
        byte[] rowVersion = [];
        var title = request.Title?.Trim() ?? string.Empty;
        var description = request.Description?.Trim();

        if (!TryParseTaskCode(request.Code, out code))
        {
            errors["code"] = ["Task code must use PREFIX-NUMBER format, for example TP-123."];
        }

        if (title.Length == 0 || title.Length > ProjectTask.TitleMaximumLength)
        {
            errors["title"] =
                [$"Title must contain 1 to {ProjectTask.TitleMaximumLength} characters."];
        }

        if (description?.Length > ProjectTask.DescriptionMaximumLength)
        {
            errors["description"] =
                [$"Description cannot exceed {ProjectTask.DescriptionMaximumLength} characters."];
        }

        if (!TryParsePriority(request.Priority, out priority))
        {
            errors["priority"] = ["Unknown task priority."];
        }

        if (request.EstimateHours is <= 0 or > ProjectTask.EstimateHoursMaximum)
        {
            errors["estimateHours"] =
                [$"Estimate must be greater than zero and no more than {ProjectTask.EstimateHoursMaximum} hours."];
        }

        ValidateOptionalId(request.AssigneeUserId, "assigneeUserId", errors);
        ValidateOptionalId(request.ReviewerUserId, "reviewerUserId", errors);
        ValidateOptionalId(request.SprintId, "sprintId", errors);

        if (requireRowVersion && !TryDecodeRowVersion(request.RowVersion, out rowVersion))
        {
            errors["rowVersion"] = ["Row version must be a valid Base64 string."];
        }

        return new SaveTaskValidation(
            errors.Count == 0 ? null : ValidationError(errors),
            code,
            priority,
            rowVersion);
    }

    private static void ValidateOptionalId(
        Guid? value,
        string field,
        IDictionary<string, string[]> errors)
    {
        if (value == Guid.Empty)
        {
            errors[field] = ["Identifier cannot be empty."];
        }
    }

    private static bool TryParseTaskCode(string? value, out TaskCode code)
    {
        try
        {
            code = TaskCode.Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            code = default;
            return false;
        }
    }

    private static bool TryParseStatus(string? value, out ProjectTaskStatus status) =>
        Enum.TryParse(NormalizeEnum(value), ignoreCase: true, out status) &&
        Enum.IsDefined(status);

    private static bool TryParsePriority(string? value, out TaskPriority priority)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            priority = TaskPriority.Medium;
            return true;
        }

        return Enum.TryParse(NormalizeEnum(value), ignoreCase: true, out priority) &&
            Enum.IsDefined(priority);
    }

    private static string NormalizeEnum(string? value) =>
        (value ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

    private static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        if (value is null)
        {
            rowVersion = [];
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult ValidationError(IReadOnlyDictionary<string, string[]> errors) =>
        Results.BadRequest(new ApiErrorResponse(
            "validation_failed",
            "Please correct the highlighted fields.",
            errors));

    private static IResult MapFailure(TaskFailure failure) => failure switch
    {
        TaskFailure.ProjectArchived => Conflict(
            "project_archived",
            "Archived projects cannot be changed."),
        TaskFailure.TaskArchived => Conflict(
            "task_archived",
            "Archived tasks cannot be changed."),
        TaskFailure.TaskCodeConflict => Conflict(
            "task_code_conflict",
            "A task with this code already exists in the project."),
        TaskFailure.TaskCodeImmutable => Conflict(
            "task_code_immutable",
            "Task code cannot be changed after creation."),
        TaskFailure.InvalidStatusTransition => Conflict(
            "invalid_status_transition",
            "This task status transition is not allowed."),
        TaskFailure.ConcurrencyConflict => Conflict(
            "task_conflict",
            "This task changed after you loaded it. Refresh and retry with the latest rowVersion."),
        TaskFailure.Forbidden => Results.Json(
            new ApiErrorResponse(
                "task_mutation_forbidden",
                "You do not have permission to perform this task transition."),
            statusCode: StatusCodes.Status403Forbidden),
        TaskFailure.AssigneeNotFound => Results.BadRequest(new ApiErrorResponse(
            "assignee_not_found",
            "Assignee must be an active member of this workspace.")),
        TaskFailure.ReviewerNotFound => Results.BadRequest(new ApiErrorResponse(
            "reviewer_not_found",
            "Reviewer must be an active member of this workspace.")),
        TaskFailure.SprintNotFound => Results.NotFound(new ApiErrorResponse(
            "sprint_not_found",
            "Sprint was not found in this project.")),
        TaskFailure.TaskNotFound => Results.NotFound(new ApiErrorResponse(
            "task_not_found",
            "Task was not found in this project.")),
        _ => Results.NotFound(new ApiErrorResponse(
            "project_not_found",
            "Project was not found.")),
    };

    private static IResult Conflict(string code, string message) =>
        Results.Conflict(new ApiErrorResponse(code, message));

    private static PagedTaskResponse ToResponse(PagedTaskResult result) =>
        new(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

    private static TaskListItemResponse ToResponse(TaskListItem task) =>
        new(
            task.Id,
            task.Code,
            task.Title,
            task.Status.ToString(),
            task.Priority.ToString(),
            task.DueOn,
            task.EstimateHours,
            ToResponse(task.Assignee),
            ToResponse(task.Reviewer),
            ToResponse(task.Sprint),
            task.IsArchived,
            task.UpdatedAtUtc,
            Convert.ToBase64String(task.RowVersion));

    private static TaskDetailsResponse ToResponse(TaskDetails task) =>
        new(
            task.Id,
            task.WorkspaceId,
            task.ProjectId,
            task.Code,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.Priority.ToString(),
            task.DueOn,
            task.EstimateHours,
            ToResponse(task.Assignee),
            ToResponse(task.Reviewer),
            ToResponse(task.Sprint),
            task.IsArchived,
            task.ArchivedAtUtc,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            Convert.ToBase64String(task.RowVersion));

    private static TaskMemberResponse? ToResponse(TaskMemberSummary? member) =>
        member is null
            ? null
            : new TaskMemberResponse(member.UserId, member.DisplayName, member.Email);

    private static TaskSprintResponse? ToResponse(TaskSprintSummary? sprint) =>
        sprint is null ? null : new TaskSprintResponse(sprint.Id, sprint.Name);

    private static Guid GetRequiredUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public sealed record SaveTaskRequest(
        string? Code,
        string? Title,
        string? Description,
        Guid? AssigneeUserId,
        Guid? ReviewerUserId,
        Guid? SprintId,
        string? Priority,
        DateOnly? DueOn,
        decimal? EstimateHours,
        string? RowVersion);

    public sealed record ChangeStatusRequest(string? Status, string? RowVersion);

    public sealed record ArchiveTaskRequest(string? RowVersion);

    private sealed record SaveTaskValidation(
        IResult? Result,
        TaskCode Code,
        TaskPriority Priority,
        byte[] RowVersion);

    private sealed record TaskMemberResponse(
        Guid UserId,
        string DisplayName,
        string Email);

    private sealed record TaskSprintResponse(Guid Id, string Name);

    private sealed record TaskListItemResponse(
        Guid Id,
        string Code,
        string Title,
        string Status,
        string Priority,
        DateOnly? DueOn,
        decimal? EstimateHours,
        TaskMemberResponse? Assignee,
        TaskMemberResponse? Reviewer,
        TaskSprintResponse? Sprint,
        bool IsArchived,
        DateTimeOffset UpdatedAtUtc,
        string RowVersion);

    private sealed record TaskDetailsResponse(
        Guid Id,
        Guid WorkspaceId,
        Guid ProjectId,
        string Code,
        string Title,
        string? Description,
        string Status,
        string Priority,
        DateOnly? DueOn,
        decimal? EstimateHours,
        TaskMemberResponse? Assignee,
        TaskMemberResponse? Reviewer,
        TaskSprintResponse? Sprint,
        bool IsArchived,
        DateTimeOffset? ArchivedAtUtc,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        string RowVersion);

    private sealed record PagedTaskResponse(
        IReadOnlyList<TaskListItemResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
