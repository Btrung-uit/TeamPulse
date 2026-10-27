using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Users;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable(
            "WorkspaceInvitations",
            table => table.HasCheckConstraint(
                "CK_WorkspaceInvitations_Role",
                "[Role] IN ('Leader', 'Member', 'Advisor')"));

        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Email)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();
        builder.Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(WorkspaceInvitation.TokenHashMaximumLength)
            .IsRequired();

        builder.HasOne(invitation => invitation.Workspace)
            .WithMany(workspace => workspace.Invitations)
            .HasForeignKey(invitation => invitation.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new
        {
            invitation.WorkspaceId,
            invitation.NormalizedEmail,
        }).IsUnique();
        builder.ConfigureAuditProperties();
    }
}
