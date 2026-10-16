using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Projects;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable(
            "Sprints",
            table => table.HasCheckConstraint(
                "CK_Sprints_DateRange",
                "[EndsOn] >= [StartsOn]"));

        builder.HasKey(sprint => sprint.Id);
        builder.HasAlternateKey(sprint => new
        {
            sprint.WorkspaceId,
            sprint.ProjectId,
            sprint.Id,
        });
        builder.Property(sprint => sprint.Id).ValueGeneratedNever();

        builder.Property(sprint => sprint.Name)
            .HasMaxLength(Sprint.NameMaximumLength)
            .IsRequired();

        builder.Property(sprint => sprint.Goal)
            .HasMaxLength(Sprint.GoalMaximumLength);

        builder.HasOne(sprint => sprint.Project)
            .WithMany(project => project.Sprints)
            .HasForeignKey(sprint => new { sprint.WorkspaceId, sprint.ProjectId })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sprint => sprint.CreatedByMember)
            .WithMany()
            .HasForeignKey(sprint => new { sprint.WorkspaceId, sprint.CreatedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sprint => new
        {
            sprint.WorkspaceId,
            sprint.ProjectId,
            sprint.Name,
        }).IsUnique();

        builder.HasIndex(sprint => new
        {
            sprint.WorkspaceId,
            sprint.StartsOn,
            sprint.EndsOn,
        });

        builder.ConfigureAuditProperties();
    }
}
