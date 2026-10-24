using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Integrations;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class GitHubPullRequestTaskConfiguration
    : IEntityTypeConfiguration<GitHubPullRequestTask>
{
    public void Configure(EntityTypeBuilder<GitHubPullRequestTask> builder)
    {
        builder.ToTable("GitHubPullRequestTasks");
        builder.HasKey(link => new { link.PullRequestId, link.TaskId });

        builder.HasOne(link => link.PullRequest)
            .WithMany(pullRequest => pullRequest.TaskLinks)
            .HasForeignKey(link => new
            {
                link.WorkspaceId,
                link.ProjectId,
                link.PullRequestId,
            })
            .HasPrincipalKey(pullRequest => new
            {
                pullRequest.WorkspaceId,
                pullRequest.ProjectId,
                pullRequest.Id,
            })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Task)
            .WithMany()
            .HasForeignKey(link => new
            {
                link.WorkspaceId,
                link.ProjectId,
                link.TaskId,
            })
            .HasPrincipalKey(task => new { task.WorkspaceId, task.ProjectId, task.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => new
        {
            link.WorkspaceId,
            link.ProjectId,
            link.TaskId,
        });
    }
}
