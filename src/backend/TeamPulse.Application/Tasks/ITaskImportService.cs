namespace TeamPulse.Application.Tasks;

public sealed record ImportColumn(int Number, string Name);

public sealed record ImportSheet(string Name, int DataRowCount, IReadOnlyList<ImportColumn> Columns);

public sealed record TaskImportUpload(
    Guid BatchId,
    string FileName,
    long FileSizeBytes,
    IReadOnlyList<ImportSheet> Sheets);

public sealed record TaskImportMapping(
    int TaskCode,
    int Title,
    int? AssigneeEmail,
    int Status,
    int Priority,
    int? DueDate,
    int? EstimateHours,
    int? DependsOn);

public sealed record TaskImportPreviewCommand(string SheetName, TaskImportMapping Mapping);

public sealed record TaskImportCellError(
    int RowNumber,
    string Column,
    string Reason,
    string? RawValue);

public sealed record TaskImportPreviewRow(
    int RowNumber,
    string? TaskCode,
    string? Title,
    string? AssigneeEmail,
    string? Status,
    string? Priority,
    string? DueDate,
    string? EstimateHours,
    string? DependsOn,
    IReadOnlyList<TaskImportCellError> Errors);

public sealed record TaskImportPreview(
    Guid BatchId,
    string Status,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<TaskImportPreviewRow> Rows);

public sealed record TaskImportConfirmation(
    Guid BatchId,
    string Status,
    int ImportedTaskCount,
    DateTimeOffset ConfirmedAtUtc,
    bool AlreadyConfirmed);

public sealed record TaskImportHistoryItem(
    Guid BatchId,
    string FileName,
    string Status,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int ImportedTaskCount,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset? ConfirmedAtUtc);

public enum TaskImportFailure
{
    None,
    ProjectNotFound,
    ProjectArchived,
    BatchNotFound,
    BatchConfirmed,
    FileTooLarge,
    InvalidFileType,
    InvalidWorkbook,
    WorkbookEmpty,
    SheetNotFound,
    InvalidMapping,
    BatchNotValidated,
    BatchHasErrors,
    ConfirmationConflict,
}

public sealed record TaskImportResult<T>(T? Value, TaskImportFailure Failure, string? Detail = null)
{
    public bool Succeeded => Failure == TaskImportFailure.None;

    public static TaskImportResult<T> Success(T value) => new(value, TaskImportFailure.None);

    public static TaskImportResult<T> Failed(TaskImportFailure failure, string? detail = null) =>
        new(default, failure, detail);
}

public interface ITaskImportService
{
    const long MaximumFileSizeBytes = 5 * 1024 * 1024;

    Task<TaskImportResult<TaskImportUpload>> UploadAsync(
        Guid workspaceId,
        Guid projectId,
        Guid uploadedByUserId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<TaskImportResult<TaskImportPreview>> PreviewAsync(
        Guid workspaceId,
        Guid projectId,
        Guid batchId,
        TaskImportPreviewCommand command,
        CancellationToken cancellationToken = default);

    Task<TaskImportResult<TaskImportConfirmation>> ConfirmAsync(
        Guid workspaceId,
        Guid projectId,
        Guid batchId,
        Guid confirmedByUserId,
        CancellationToken cancellationToken = default);

    Task<TaskImportResult<IReadOnlyList<TaskImportHistoryItem>>> ListHistoryAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}
