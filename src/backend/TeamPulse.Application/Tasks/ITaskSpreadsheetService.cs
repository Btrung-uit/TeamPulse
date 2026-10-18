using TeamPulse.Domain.Tasks;

namespace TeamPulse.Application.Tasks;

public enum TaskExportSort
{
    DueAscending,
    UpdatedDescending,
    CodeAscending,
    PriorityDescending,
}

public sealed record TaskSpreadsheetQuery(
    string? Search,
    ProjectTaskStatus? Status,
    TaskPriority? Priority,
    Guid? AssigneeUserId,
    Guid? SprintId,
    DateOnly? DueBefore,
    bool IncludeArchived,
    bool OpenOnly,
    bool AttentionOnly,
    TaskExportSort Sort);

public sealed record TaskSpreadsheetFile(
    string FileName,
    string ContentType,
    byte[] Content);

public interface ITaskSpreadsheetService
{
    Task<TaskResult<TaskSpreadsheetFile>> CreateTemplateAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<TaskResult<TaskSpreadsheetFile>> ExportAsync(
        Guid workspaceId,
        Guid projectId,
        TaskSpreadsheetQuery query,
        CancellationToken cancellationToken = default);
}
