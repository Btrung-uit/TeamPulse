using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Notifications;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(notification => notification.Title)
            .HasMaxLength(Notification.TitleMaximumLength)
            .IsRequired();
        builder.Property(notification => notification.Message)
            .HasMaxLength(Notification.MessageMaximumLength)
            .IsRequired();
        builder.Ignore(notification => notification.IsRead);

        builder.HasOne<WorkspaceMember>()
            .WithMany()
            .HasForeignKey(notification => new
            {
                notification.WorkspaceId,
                notification.RecipientUserId,
            })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(notification => new
        {
            notification.WorkspaceId,
            notification.RecipientUserId,
            notification.ReadAtUtc,
            notification.CreatedAtUtc,
        });
        builder.ConfigureAuditProperties();
    }
}
