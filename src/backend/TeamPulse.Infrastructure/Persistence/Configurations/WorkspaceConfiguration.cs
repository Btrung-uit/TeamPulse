using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Users;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Id).ValueGeneratedNever();

        builder.Property(workspace => workspace.Name)
            .HasMaxLength(Workspace.NameMaximumLength)
            .IsRequired();

        builder.HasOne(workspace => workspace.CreatedByUser)
            .WithMany(user => user.CreatedWorkspaces)
            .HasForeignKey(workspace => workspace.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workspace => workspace.CreatedByUserId);
        builder.ConfigureAuditProperties();
    }
}
