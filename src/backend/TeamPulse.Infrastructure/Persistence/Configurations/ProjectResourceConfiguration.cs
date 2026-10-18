using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Resources;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class ProjectResourceConfiguration : IEntityTypeConfiguration<ProjectResource>
{
    public void Configure(EntityTypeBuilder<ProjectResource> builder)
    {
        builder.ToTable("ProjectResources");
        builder.HasKey(resource => resource.Id);
        builder.HasAlternateKey(resource => new
        {
            resource.WorkspaceId,
            resource.ProjectId,
            resource.Id,
        });
        builder.Property(resource => resource.Id).ValueGeneratedNever();
        builder.Property(resource => resource.Title)
            .HasMaxLength(ProjectResource.TitleMaximumLength)
            .IsRequired();
        builder.Property(resource => resource.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(resource => resource.Url)
            .HasMaxLength(ProjectResource.UrlMaximumLength)
            .IsRequired();
        builder.Property(resource => resource.ExternalId)
            .HasMaxLength(ProjectResource.ExternalIdMaximumLength);

        builder.HasOne(resource => resource.Project)
            .WithMany()
            .HasForeignKey(resource => new { resource.WorkspaceId, resource.ProjectId })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(resource => resource.OwnerMember)
            .WithMany()
            .HasForeignKey(resource => new { resource.WorkspaceId, resource.OwnerUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(resource => resource.LinkedTask)
            .WithMany()
            .HasForeignKey(resource => new
            {
                resource.WorkspaceId,
                resource.ProjectId,
                resource.LinkedTaskId,
            })
            .HasPrincipalKey(task => new { task.WorkspaceId, task.ProjectId, task.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(resource => new
        {
            resource.WorkspaceId,
            resource.ProjectId,
            resource.Type,
            resource.UpdatedAtUtc,
        });
        builder.ConfigureAuditProperties();
    }
}
