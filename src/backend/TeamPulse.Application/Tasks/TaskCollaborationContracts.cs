using TeamPulse.Domain.Tasks;

namespace TeamPulse.Application.Tasks;

public enum TaskReviewDecision
{
    Accept,
    RequestChanges,
}

public sealed record AddTaskEvidenceCommand(
    TaskEvidenceType Type,
    string Url,
    string? Note);

public sealed record ReviewTaskCommand(
    TaskReviewDecision Decision,
    string Comment,
    byte[] ExpectedRowVersion);

public sealed record TaskEvidenceItem(
    Guid Id,
    TaskEvidenceType Type,
    string Url,
    string? Note,
    TaskMemberSummary SubmittedBy,
    DateTimeOffset CreatedAtUtc);

public sealed record TaskCommentItem(
    Guid Id,
    TaskCommentKind Kind,
    string Body,
    TaskMemberSummary Author,
    DateTimeOffset CreatedAtUtc);

public sealed record TaskCollaborationDetails(
    IReadOnlyList<TaskEvidenceItem> Evidence,
    IReadOnlyList<TaskCommentItem> Comments);

public sealed record TaskReviewResult(
    Guid TaskId,
    ProjectTaskStatus Status,
    byte[] RowVersion,
    TaskCommentItem Comment);
