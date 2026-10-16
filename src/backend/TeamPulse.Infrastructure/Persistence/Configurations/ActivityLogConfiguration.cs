using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Activities;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Id).ValueGeneratedNever();

        builder.Property(activity => activity.Action)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();
        builder.Property(activity => activity.EntityType)
            .HasMaxLength(ActivityLog.EntityTypeMaximumLength)
            .IsRequired();
        builder.Property(activity => activity.PreviousValue)
            .HasMaxLength(ActivityLog.ValueMaximumLength);
        builder.Property(activity => activity.NewValue)
            .HasMaxLength(ActivityLog.ValueMaximumLength);
        builder.Property(activity => activity.OccurredAtUtc).HasPrecision(7);

        builder.HasOne(activity => activity.Workspace)
            .WithMany(workspace => workspace.Activities)
            .HasForeignKey(activity => activity.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(activity => activity.ActorUser)
            .WithMany()
            .HasForeignKey(activity => activity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(activity => activity.SubjectUser)
            .WithMany()
            .HasForeignKey(activity => activity.SubjectUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(activity => new { activity.WorkspaceId, activity.OccurredAtUtc });
        builder.HasIndex(activity => new
        {
            activity.WorkspaceId,
            activity.EntityType,
            activity.EntityId,
        });
        builder.HasIndex(activity => activity.ActorUserId);
        builder.HasIndex(activity => activity.SubjectUserId);
    }
}
