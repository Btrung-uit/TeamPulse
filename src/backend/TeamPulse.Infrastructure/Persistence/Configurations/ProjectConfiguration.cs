using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Projects;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable(
            "Projects",
            table => table.HasCheckConstraint(
                "CK_Projects_ArchiveState",
                "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR " +
                "([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)"));

        builder.HasKey(project => project.Id);
        builder.HasAlternateKey(project => new { project.WorkspaceId, project.Id });
        builder.Property(project => project.Id).ValueGeneratedNever();

        builder.Property(project => project.Name)
            .HasMaxLength(Project.NameMaximumLength)
            .IsRequired();

        builder.Property(project => project.Description)
            .HasMaxLength(Project.DescriptionMaximumLength);

        builder.Property(project => project.ArchivedAtUtc).HasPrecision(7);

        builder.HasOne(project => project.Workspace)
            .WithMany(workspace => workspace.Projects)
            .HasForeignKey(project => project.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(project => project.CreatedByMember)
            .WithMany()
            .HasForeignKey(project => new { project.WorkspaceId, project.CreatedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(project => new
        {
            project.WorkspaceId,
            project.IsArchived,
            project.UpdatedAtUtc,
        });

        builder.ConfigureAuditProperties();
    }
}
