using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Authorization;
using TeamPulse.Api.Security;

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
        group.MapGet("/template", DownloadTemplateAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapGet("/export", ExportAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPost("/imports", UploadImportAsync)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024))
            .RequireRateLimiting(SecurityPolicies.ImportRateLimit)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapGet("/imports", ListImportHistoryAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPut("/imports/{batchId:guid}/preview", PreviewImportAsync)
            .RequireRateLimiting(SecurityPolicies.ImportRateLimit)
            .RequireAuthorization(WorkspacePolicies.Leader);
        group.MapPost("/imports/{batchId:guid}/confirm", ConfirmImportAsync)
            .RequireRateLimiting(SecurityPolicies.ImportRateLimit)
            .RequireAuthorization(WorkspacePolicies.Leader);
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

    private static async Task<IResult> UploadImportAsync(
        Guid workspaceId,
        Guid projectId,
        IFormFile? file,
        ClaimsPrincipal principal,
        ITaskImportService importService,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return ValidationError(new Dictionary<string, string[]>
            {
                ["file"] = ["An .xlsx workbook is required."],
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await importService.UploadAsync(
            workspaceId,
            projectId,
            GetRequiredUserId(principal),
            file.FileName,
            stream,
            cancellationToken);
        return result.Succeeded
            ? Results.Created(
                $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks/imports/{result.Value!.BatchId}",
                result.Value)
            : MapImportFailure(result.Failure, result.Detail);
    }

    private static async Task<IResult> PreviewImportAsync(
        Guid workspaceId,
        Guid projectId,
        Guid batchId,
        PreviewTaskImportRequest request,
        ITaskImportService importService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SheetName) || request.Mapping is null)
        {
            return ValidationError(new Dictionary<string, string[]>
            {
                ["mapping"] = ["A sheet and column mapping are required."],
            });
        }

        var mapping = request.Mapping;
        var result = await importService.PreviewAsync(
            workspaceId,
            projectId,
            batchId,
            new TaskImportPreviewCommand(
                request.SheetName.Trim(),
                new TaskImportMapping(
                    mapping.TaskCode,
                    mapping.Title,
                    mapping.AssigneeEmail,
                    mapping.Status,
                    mapping.Priority,
                    mapping.DueDate,
                    mapping.EstimateHours,
                    mapping.DependsOn)),
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : MapImportFailure(result.Failure, result.Detail);
    }

    private static async Task<IResult> ConfirmImportAsync(
        Guid workspaceId,
        Guid projectId,
        Guid batchId,
        ClaimsPrincipal principal,
        ITaskImportService importService,
        CancellationToken cancellationToken)
    {
        var result = await importService.ConfirmAsync(
            workspaceId,
            projectId,
            batchId,
            GetRequiredUserId(principal),
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : MapImportFailure(result.Failure, result.Detail);
    }

    private static async Task<IResult> ListImportHistoryAsync(
        Guid workspaceId,
        Guid projectId,
        ITaskImportService importService,
        CancellationToken cancellationToken)
    {
        var result = await importService.ListHistoryAsync(
            workspaceId,
            projectId,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : MapImportFailure(result.Failure, result.Detail);
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

    private static async Task<IResult> DownloadTemplateAsync(
        Guid workspaceId,
        Guid projectId,
        ITaskSpreadsheetService spreadsheetService,
        CancellationToken cancellationToken)
    {
        var result = await spreadsheetService.CreateTemplateAsync(
            workspaceId,
            projectId,
            cancellationToken);
        return result.Succeeded
            ? ToFileResult(result.Value!)
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> ExportAsync(
        Guid workspaceId,
        Guid projectId,
        string? search,
        string? status,
        string? priority,
        Guid? assigneeUserId,
        Guid? sprintId,
        DateOnly? dueBefore,
        bool? includeArchived,
        string? view,
        bool? attention,
        string? sort,
        ITaskSpreadsheetService spreadsheetService,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ProjectTaskStatus? parsedStatus = null;
        TaskPriority? parsedPriority = null;

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

        var openOnly = string.Equals(view, "open", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(view) && !openOnly)
        {
            errors["view"] = ["Unknown task view."];
        }

        if (!TryParseExportSort(sort, out var parsedSort))
        {
            errors["sort"] = ["Unknown task sort."];
        }

        if (errors.Count > 0)
        {
            return ValidationError(errors);
        }

        var result = await spreadsheetService.ExportAsync(
            workspaceId,
            projectId,
            new TaskSpreadsheetQuery(
                NormalizeOptional(search),
                parsedStatus,
                parsedPriority,
                assigneeUserId,
                sprintId,
                dueBefore,
                includeArchived ?? false,
                openOnly,
                attention ?? false,
                parsedSort),
            cancellationToken);
        return result.Succeeded
            ? ToFileResult(result.Value!)
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

    private static bool TryParseExportSort(string? value, out TaskExportSort sort)
    {
        sort = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "due-asc" => TaskExportSort.DueAscending,
            "updated-desc" => TaskExportSort.UpdatedDescending,
            "code-asc" => TaskExportSort.CodeAscending,
            "priority-desc" => TaskExportSort.PriorityDescending,
            _ => (TaskExportSort)(-1),
        };
        return Enum.IsDefined(sort);
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

    private static IResult ToFileResult(TaskSpreadsheetFile file) =>
        Results.File(file.Content, file.ContentType, file.FileName);

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

    private static IResult MapImportFailure(TaskImportFailure failure, string? detail) => failure switch
    {
        TaskImportFailure.ProjectArchived => Conflict(
            "project_archived",
            "Archived projects cannot be changed."),
        TaskImportFailure.BatchConfirmed => Conflict(
            "import_already_confirmed",
            "A confirmed import cannot be previewed again."),
        TaskImportFailure.FileTooLarge => Results.Json(
            new ApiErrorResponse("import_file_too_large", "The workbook cannot exceed 5 MB."),
            statusCode: StatusCodes.Status413PayloadTooLarge),
        TaskImportFailure.InvalidFileType => Results.BadRequest(new ApiErrorResponse(
            "invalid_import_file_type",
            "Only .xlsx workbooks are supported.")),
        TaskImportFailure.InvalidWorkbook or TaskImportFailure.WorkbookEmpty =>
            Results.BadRequest(new ApiErrorResponse(
                "invalid_import_workbook",
                detail ?? "The workbook could not be read.")),
        TaskImportFailure.SheetNotFound => Results.BadRequest(new ApiErrorResponse(
            "import_sheet_not_found",
            "The selected sheet was not found in this workbook.")),
        TaskImportFailure.InvalidMapping => Results.BadRequest(new ApiErrorResponse(
            "invalid_import_mapping",
            detail ?? "The column mapping is invalid.")),
        TaskImportFailure.BatchNotValidated => Conflict(
            "import_not_validated",
            "Preview and validate the import before confirming it."),
        TaskImportFailure.BatchHasErrors => Conflict(
            "import_has_errors",
            detail ?? "Resolve every validation error before confirming the import."),
        TaskImportFailure.ConfirmationConflict => Conflict(
            "import_confirmation_conflict",
            detail ?? "The import changed while it was being confirmed. Refresh and try again."),
        TaskImportFailure.BatchNotFound => Results.NotFound(new ApiErrorResponse(
            "import_batch_not_found",
            "The import batch was not found in this project.")),
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
            task.StatusBeforeBlocked?.ToString(),
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

    public sealed record PreviewTaskImportRequest(
        string? SheetName,
        TaskImportMappingRequest? Mapping);

    public sealed record TaskImportMappingRequest(
        int TaskCode,
        int Title,
        int? AssigneeEmail,
        int Status,
        int Priority,
        int? DueDate,
        int? EstimateHours,
        int? DependsOn);

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
        string? StatusBeforeBlocked,
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
