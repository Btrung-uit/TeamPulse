using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Tasks;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).ValueGeneratedNever();
        builder.Property(comment => comment.Kind)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(comment => comment.Body)
            .HasMaxLength(TaskComment.BodyMaximumLength)
            .IsRequired();

        builder.HasOne(comment => comment.Task)
            .WithMany(task => task.Comments)
            .HasForeignKey(comment => new
            {
                comment.WorkspaceId,
                comment.ProjectId,
                comment.TaskId,
            })
            .HasPrincipalKey(task => new { task.WorkspaceId, task.ProjectId, task.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.AuthorMember)
            .WithMany()
            .HasForeignKey(comment => new { comment.WorkspaceId, comment.AuthorUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(comment => new
        {
            comment.WorkspaceId,
            comment.ProjectId,
            comment.TaskId,
            comment.CreatedAtUtc,
        });
        builder.ConfigureAuditProperties();
    }
}
