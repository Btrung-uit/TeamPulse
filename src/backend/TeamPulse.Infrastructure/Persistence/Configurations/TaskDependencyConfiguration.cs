using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Tasks;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.ToTable(
            "TaskDependencies",
            table => table.HasCheckConstraint(
                "CK_TaskDependencies_DifferentTasks",
                "[PredecessorTaskId] <> [SuccessorTaskId]"));

        builder.HasKey(dependency => new
        {
            dependency.WorkspaceId,
            dependency.ProjectId,
            dependency.PredecessorTaskId,
            dependency.SuccessorTaskId,
        });

        builder.HasOne(dependency => dependency.PredecessorTask)
            .WithMany(task => task.SuccessorDependencies)
            .HasForeignKey(dependency => new
            {
                dependency.WorkspaceId,
                dependency.ProjectId,
                dependency.PredecessorTaskId,
            })
            .HasPrincipalKey(task => new { task.WorkspaceId, task.ProjectId, task.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dependency => dependency.SuccessorTask)
            .WithMany(task => task.PredecessorDependencies)
            .HasForeignKey(dependency => new
            {
                dependency.WorkspaceId,
                dependency.ProjectId,
                dependency.SuccessorTaskId,
            })
            .HasPrincipalKey(task => new { task.WorkspaceId, task.ProjectId, task.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dependency => new
        {
            dependency.WorkspaceId,
            dependency.ProjectId,
            dependency.SuccessorTaskId,
        });

        builder.ConfigureAuditProperties();
    }
}
