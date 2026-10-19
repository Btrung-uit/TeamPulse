using TeamPulse.Domain.Common;

namespace TeamPulse.Domain.Imports;

public sealed class ImportErrorRow : AuditableEntity
{
    public const int ColumnMaximumLength = 64;
    public const int ReasonMaximumLength = 500;
    public const int RawValueMaximumLength = 1_000;

    private ImportErrorRow()
    {
    }

    public ImportErrorRow(
        Guid id,
        Guid importBatchId,
        int rowNumber,
        string column,
        string reason,
        string? rawValue)
        : base(id)
    {
        if (importBatchId == Guid.Empty)
        {
            throw new ArgumentException("Import batch is required.", nameof(importBatchId));
        }

        if (rowNumber < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber));
        }

        ImportBatchId = importBatchId;
        RowNumber = rowNumber;
        Column = DomainText.Required(column, nameof(column), ColumnMaximumLength);
        Reason = DomainText.Required(reason, nameof(reason), ReasonMaximumLength);
        RawValue = DomainText.Optional(rawValue, nameof(rawValue), RawValueMaximumLength);
    }

    public Guid ImportBatchId { get; private set; }

    public int RowNumber { get; private set; }

    public string Column { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public string? RawValue { get; private set; }

    public ImportBatch ImportBatch { get; private set; } = null!;
}
