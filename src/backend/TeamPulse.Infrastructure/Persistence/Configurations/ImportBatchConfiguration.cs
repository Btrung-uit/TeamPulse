using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Imports;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).ValueGeneratedNever();
        builder.Property(batch => batch.FileName).HasMaxLength(ImportBatch.FileNameMaximumLength).IsRequired();
        builder.Property(batch => batch.ContentType).HasMaxLength(ImportBatch.ContentTypeMaximumLength).IsUnicode(false).IsRequired();
        builder.Property(batch => batch.Content).IsRequired();
        builder.Property(batch => batch.Sha256).HasMaxLength(ImportBatch.Sha256MaximumLength).IsUnicode(false).IsRequired();
        builder.Property(batch => batch.SelectedSheet).HasMaxLength(ImportBatch.SheetNameMaximumLength);
        builder.Property(batch => batch.MappingJson).HasMaxLength(ImportBatch.MappingJsonMaximumLength).IsUnicode(false);
        builder.Property(batch => batch.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(batch => batch.ConfirmedAtUtc).HasPrecision(7);
        builder.Property(batch => batch.RowVersion).IsRowVersion();

        builder.HasOne<TeamPulse.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(batch => new { batch.WorkspaceId, batch.ProjectId })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TeamPulse.Domain.Workspaces.WorkspaceMember>()
            .WithMany()
            .HasForeignKey(batch => new { batch.WorkspaceId, batch.UploadedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TeamPulse.Domain.Workspaces.WorkspaceMember>()
            .WithMany()
            .HasForeignKey(batch => new { batch.WorkspaceId, batch.ConfirmedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(batch => new { batch.WorkspaceId, batch.ProjectId, batch.CreatedAtUtc });
        builder.ConfigureAuditProperties();
    }
}
