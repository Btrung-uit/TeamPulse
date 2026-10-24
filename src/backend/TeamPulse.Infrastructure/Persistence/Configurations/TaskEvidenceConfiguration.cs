using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Tasks;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class TaskEvidenceConfiguration : IEntityTypeConfiguration<TaskEvidence>
{
    public void Configure(EntityTypeBuilder<TaskEvidence> builder)
    {
        builder.ToTable("TaskEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Id).ValueGeneratedNever();
        builder.Property(evidence => evidence.Type)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(evidence => evidence.Source)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(TaskEvidenceSource.Manual)
            .IsRequired();
        builder.Property(evidence => evidence.Url)
            .HasMaxLength(TaskEvidence.UrlMaximumLength)
            .IsRequired();
        builder.Property(evidence => evidence.Note)
            .HasMaxLength(TaskEvidence.NoteMaximumLength);
        builder.Property(evidence => evidence.ExternalId)
            .HasMaxLength(TaskEvidence.ExternalIdMaximumLength);

        builder.HasOne(evidence => evidence.Task)
            .WithMany(task => task.Evidence)
            .HasForeignKey(evidence => new
            {
                evidence.WorkspaceId,
                evidence.ProjectId,
                evidence.TaskId,
            })
            .HasPrincipalKey(task => new { task.WorkspaceId, task.ProjectId, task.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(evidence => evidence.SubmittedByMember)
            .WithMany()
            .HasForeignKey(evidence => new { evidence.WorkspaceId, evidence.SubmittedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(evidence => new
        {
            evidence.WorkspaceId,
            evidence.ProjectId,
            evidence.TaskId,
            evidence.CreatedAtUtc,
        });
        builder.HasIndex(evidence => new
        {
            evidence.WorkspaceId,
            evidence.ProjectId,
            evidence.TaskId,
            evidence.Source,
            evidence.ExternalId,
        })
            .IsUnique()
            .HasFilter("[ExternalId] IS NOT NULL");
        builder.ConfigureAuditProperties();
    }
}
