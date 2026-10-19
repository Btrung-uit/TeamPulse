using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Imports;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class ImportErrorRowConfiguration : IEntityTypeConfiguration<ImportErrorRow>
{
    public void Configure(EntityTypeBuilder<ImportErrorRow> builder)
    {
        builder.ToTable("ImportErrorRows");
        builder.HasKey(error => error.Id);
        builder.Property(error => error.Id).ValueGeneratedNever();
        builder.Property(error => error.Column).HasMaxLength(ImportErrorRow.ColumnMaximumLength).IsRequired();
        builder.Property(error => error.Reason).HasMaxLength(ImportErrorRow.ReasonMaximumLength).IsRequired();
        builder.Property(error => error.RawValue).HasMaxLength(ImportErrorRow.RawValueMaximumLength);
        builder.HasOne(error => error.ImportBatch)
            .WithMany(batch => batch.Errors)
            .HasForeignKey(error => error.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(error => new { error.ImportBatchId, error.RowNumber });
        builder.ConfigureAuditProperties();
    }
}
