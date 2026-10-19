using TeamPulse.Domain.Common;

namespace TeamPulse.Domain.Imports;

public sealed class ImportBatch : AuditableEntity
{
    public const int FileNameMaximumLength = 255;
    public const int ContentTypeMaximumLength = 128;
    public const int SheetNameMaximumLength = 128;
    public const int MappingJsonMaximumLength = 2_000;
    public const int Sha256MaximumLength = 64;

    private ImportBatch()
    {
    }

    public ImportBatch(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        byte[] content,
        string sha256)
        : base(id)
    {
        if (workspaceId == Guid.Empty || projectId == Guid.Empty || uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Import scope and uploader are required.");
        }

        ArgumentNullException.ThrowIfNull(content);
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        UploadedByUserId = uploadedByUserId;
        FileName = DomainText.Required(fileName, nameof(fileName), FileNameMaximumLength);
        ContentType = DomainText.Required(contentType, nameof(contentType), ContentTypeMaximumLength);
        Content = content.ToArray();
        FileSizeBytes = content.LongLength;
        Sha256 = DomainText.Required(sha256, nameof(sha256), Sha256MaximumLength);
        Status = ImportBatchStatus.Uploaded;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public byte[] Content { get; private set; } = [];

    public string Sha256 { get; private set; } = string.Empty;

    public string? SelectedSheet { get; private set; }

    public string? MappingJson { get; private set; }

    public ImportBatchStatus Status { get; private set; }

    public int TotalRows { get; private set; }

    public int ValidRows { get; private set; }

    public int ErrorRows { get; private set; }

    public Guid? ConfirmedByUserId { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    public int ImportedTaskCount { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<ImportErrorRow> Errors { get; } = [];

    public void RecordPreview(
        string sheetName,
        string mappingJson,
        int totalRows,
        int validRows,
        int errorRows)
    {
        if (Status == ImportBatchStatus.Confirmed)
        {
            throw new InvalidOperationException("A confirmed import cannot be previewed again.");
        }

        SelectedSheet = DomainText.Required(
            sheetName,
            nameof(sheetName),
            SheetNameMaximumLength);
        MappingJson = DomainText.Required(
            mappingJson,
            nameof(mappingJson),
            MappingJsonMaximumLength);
        TotalRows = totalRows;
        ValidRows = validRows;
        ErrorRows = errorRows;
        Status = errorRows == 0 ? ImportBatchStatus.Validated : ImportBatchStatus.ValidationFailed;
    }

    public void MarkConfirmed(Guid confirmedByUserId, DateTimeOffset confirmedAtUtc, int taskCount)
    {
        if (Status == ImportBatchStatus.Confirmed)
        {
            return;
        }

        if (Status != ImportBatchStatus.Validated || ErrorRows != 0)
        {
            throw new InvalidOperationException("Only a valid import batch can be confirmed.");
        }

        if (confirmedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Confirmer is required.", nameof(confirmedByUserId));
        }

        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = confirmedAtUtc;
        ImportedTaskCount = taskCount;
        Status = ImportBatchStatus.Confirmed;
    }
}
