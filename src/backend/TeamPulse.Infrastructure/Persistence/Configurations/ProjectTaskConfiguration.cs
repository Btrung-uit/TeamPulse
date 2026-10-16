using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Tasks;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable(
            "Tasks",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Tasks_Status",
                    "[Status] IN ('Todo', 'InProgress', 'EvidenceLinked', 'InReview', " +
                    "'NeedsChanges', 'Blocked', 'Done')");
                table.HasCheckConstraint(
                    "CK_Tasks_Priority",
                    "[Priority] IN ('Low', 'Medium', 'High', 'Urgent')");
                table.HasCheckConstraint(
                    "CK_Tasks_ArchiveState",
                    "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR " +
                    "([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_Tasks_BlockedState",
                    "([Status] = 'Blocked' AND [StatusBeforeBlocked] IS NOT NULL) OR " +
                    "([Status] <> 'Blocked' AND [StatusBeforeBlocked] IS NULL)");
                table.HasCheckConstraint(
                    "CK_Tasks_EstimateHours",
                    "[EstimateHours] IS NULL OR " +
                    $"([EstimateHours] > 0 AND [EstimateHours] <= {ProjectTask.EstimateHoursMaximum})");
            });

        builder.HasKey(task => task.Id);
        builder.HasAlternateKey(task => new { task.WorkspaceId, task.ProjectId, task.Id });
        builder.Property(task => task.Id).ValueGeneratedNever();

        builder.Property(task => task.Code)
            .HasConversion(
                code => code.Value,
                value => TaskCode.Parse(value))
            .HasMaxLength(TaskCode.MaximumLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(task => task.Title)
            .HasMaxLength(ProjectTask.TitleMaximumLength)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasMaxLength(ProjectTask.DescriptionMaximumLength);

        builder.Property(task => task.Status)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(task => task.StatusBeforeBlocked)
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(task => task.Priority)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(task => task.DueOn).HasColumnType("date");
        builder.Property(task => task.EstimateHours).HasPrecision(6, 2);
        builder.Property(task => task.ArchivedAtUtc).HasPrecision(7);
        builder.Property(task => task.RowVersion).IsRowVersion();

        builder.HasOne(task => task.Project)
            .WithMany(project => project.Tasks)
            .HasForeignKey(task => new { task.WorkspaceId, task.ProjectId })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(task => task.Sprint)
            .WithMany(sprint => sprint.Tasks)
            .HasForeignKey(task => new { task.WorkspaceId, task.ProjectId, task.SprintId })
            .HasPrincipalKey(sprint => new { sprint.WorkspaceId, sprint.ProjectId, sprint.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(task => task.CreatedByMember)
            .WithMany()
            .HasForeignKey(task => new { task.WorkspaceId, task.CreatedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(task => task.AssigneeMember)
            .WithMany()
            .HasForeignKey(task => new { task.WorkspaceId, task.AssigneeUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(task => task.ReviewerMember)
            .WithMany()
            .HasForeignKey(task => new { task.WorkspaceId, task.ReviewerUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(task => new { task.WorkspaceId, task.ProjectId, task.Code })
            .IsUnique();

        builder.HasIndex(task => new
        {
            task.WorkspaceId,
            task.ProjectId,
            task.IsArchived,
            task.Status,
            task.DueOn,
        });

        builder.HasIndex(task => new { task.WorkspaceId, task.AssigneeUserId, task.Status });
        builder.ConfigureAuditProperties();
    }
}
