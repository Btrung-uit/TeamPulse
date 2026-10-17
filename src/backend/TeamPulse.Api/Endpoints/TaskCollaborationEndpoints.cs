using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Authorization;

namespace TeamPulse.Api.Endpoints;

public static class TaskCollaborationEndpoints
{
    public static IEndpointRouteBuilder MapTaskCollaborationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/tasks/{taskId:guid}")
            .WithTags("Task collaboration")
            .RequireAuthorization(WorkspacePolicies.Member);

        group.MapGet("/collaboration", GetAsync);
        group.MapPost("/evidence", AddEvidenceAsync);
        group.MapPost("/review", ReviewAsync)
            .RequireAuthorization(WorkspacePolicies.Leader);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        ITaskCollaborationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(
            workspaceId,
            projectId,
            taskId,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> AddEvidenceAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        AddEvidenceRequest request,
        ClaimsPrincipal principal,
        ITaskCollaborationService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<TaskEvidenceType>(request.Type, true, out var type) ||
            !Enum.IsDefined(type))
        {
            errors["type"] = ["Unknown evidence type."];
        }

        var url = request.Url?.Trim() ?? string.Empty;
        if (url.Length == 0 || url.Length > TaskEvidence.UrlMaximumLength ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors["url"] = ["Enter an absolute HTTP or HTTPS URL."];
        }

        var note = NormalizeOptional(request.Note);
        if (note?.Length > TaskEvidence.NoteMaximumLength)
        {
            errors["note"] = [$"Note cannot exceed {TaskEvidence.NoteMaximumLength} characters."];
        }

        if (errors.Count > 0)
        {
            return ValidationError(errors);
        }

        var result = await service.AddEvidenceAsync(
            workspaceId,
            projectId,
            taskId,
            GetRequiredUserId(principal),
            new AddTaskEvidenceCommand(type, url, note),
            cancellationToken);
        return result.Succeeded
            ? Results.Created(
                $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks/{taskId}/collaboration",
                ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static async Task<IResult> ReviewAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        ReviewTaskRequest request,
        ClaimsPrincipal principal,
        ITaskCollaborationService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<TaskReviewDecision>(request.Decision, true, out var decision) ||
            !Enum.IsDefined(decision))
        {
            errors["decision"] = ["Decision must be Accept or RequestChanges."];
        }

        var comment = request.Comment?.Trim() ?? string.Empty;
        if (comment.Length == 0 || comment.Length > TaskComment.BodyMaximumLength)
        {
            errors["comment"] =
                [$"Comment must contain 1 to {TaskComment.BodyMaximumLength} characters."];
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            errors["rowVersion"] = ["Row version must be a valid Base64 string."];
        }

        if (errors.Count > 0)
        {
            return ValidationError(errors);
        }

        var result = await service.ReviewAsync(
            workspaceId,
            projectId,
            taskId,
            GetRequiredUserId(principal),
            new ReviewTaskCommand(decision, comment, rowVersion),
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToResponse(result.Value!))
            : MapFailure(result.Failure);
    }

    private static IResult MapFailure(TaskFailure failure) => failure switch
    {
        TaskFailure.ProjectArchived => Conflict(
            "project_archived",
            "Archived projects cannot be changed."),
        TaskFailure.TaskArchived => Conflict(
            "task_archived",
            "Archived tasks cannot be changed."),
        TaskFailure.ReviewRequiresInReview => Conflict(
            "review_requires_in_review",
            "The task must be in review before a decision can be recorded."),
        TaskFailure.ConcurrencyConflict => Conflict(
            "task_conflict",
            "This task changed after you loaded it. Refresh and retry with the latest rowVersion."),
        TaskFailure.Forbidden => Results.Json(
            new ApiErrorResponse(
                "task_collaboration_forbidden",
                "You do not have permission to change evidence or review this task."),
            statusCode: StatusCodes.Status403Forbidden),
        TaskFailure.TaskNotFound => Results.NotFound(new ApiErrorResponse(
            "task_not_found",
            "Task was not found in this project.")),
        _ => Results.NotFound(new ApiErrorResponse(
            "project_not_found",
            "Project was not found.")),
    };

    private static IResult ValidationError(IReadOnlyDictionary<string, string[]> errors) =>
        Results.BadRequest(new ApiErrorResponse(
            "validation_failed",
            "Please correct the highlighted fields.",
            errors));

    private static IResult Conflict(string code, string message) =>
        Results.Conflict(new ApiErrorResponse(code, message));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static Guid GetRequiredUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static TaskCollaborationResponse ToResponse(TaskCollaborationDetails details) =>
        new(
            details.Evidence.Select(ToResponse).ToArray(),
            details.Comments.Select(ToResponse).ToArray());

    private static TaskEvidenceResponse ToResponse(TaskEvidenceItem evidence) =>
        new(
            evidence.Id,
            evidence.Type.ToString(),
            evidence.Url,
            evidence.Note,
            ToResponse(evidence.SubmittedBy),
            evidence.CreatedAtUtc);

    private static TaskCommentResponse ToResponse(TaskCommentItem comment) =>
        new(
            comment.Id,
            comment.Kind.ToString(),
            comment.Body,
            ToResponse(comment.Author),
            comment.CreatedAtUtc);

    private static TaskReviewResponse ToResponse(TaskReviewResult review) =>
        new(
            review.TaskId,
            review.Status.ToString(),
            Convert.ToBase64String(review.RowVersion),
            ToResponse(review.Comment));

    private static TaskMemberResponse ToResponse(TaskMemberSummary member) =>
        new(member.UserId, member.DisplayName, member.Email);

    public sealed record AddEvidenceRequest(string? Type, string? Url, string? Note);
    public sealed record ReviewTaskRequest(
        string? Decision,
        string? Comment,
        string? RowVersion);
    private sealed record TaskMemberResponse(Guid UserId, string DisplayName, string Email);
    private sealed record TaskEvidenceResponse(
        Guid Id,
        string Type,
        string Url,
        string? Note,
        TaskMemberResponse SubmittedBy,
        DateTimeOffset CreatedAtUtc);
    private sealed record TaskCommentResponse(
        Guid Id,
        string Kind,
        string Body,
        TaskMemberResponse Author,
        DateTimeOffset CreatedAtUtc);
    private sealed record TaskCollaborationResponse(
        IReadOnlyList<TaskEvidenceResponse> Evidence,
        IReadOnlyList<TaskCommentResponse> Comments);
    private sealed record TaskReviewResponse(
        Guid TaskId,
        string Status,
        string RowVersion,
        TaskCommentResponse Comment);
}
