using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable(
            "WorkspaceMembers",
            table => table.HasCheckConstraint(
                "CK_WorkspaceMembers_Role",
                "[Role] IN ('Leader', 'Member', 'Advisor')"));

        builder.HasKey(member => new { member.WorkspaceId, member.UserId });

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(member => member.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(member => member.Workspace)
            .WithMany(workspace => workspace.Members)
            .HasForeignKey(member => member.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany(user => user.WorkspaceMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(member => new { member.UserId, member.IsActive });
        builder.ConfigureAuditProperties();
    }
}
