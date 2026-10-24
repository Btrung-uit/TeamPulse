using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Integrations;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class GitHubPullRequestConfiguration
    : IEntityTypeConfiguration<GitHubPullRequest>
{
    public void Configure(EntityTypeBuilder<GitHubPullRequest> builder)
    {
        builder.ToTable("GitHubPullRequests");
        builder.HasKey(pullRequest => pullRequest.Id);
        builder.HasAlternateKey(pullRequest => new
        {
            pullRequest.WorkspaceId,
            pullRequest.ProjectId,
            pullRequest.Id,
        });
        builder.Property(pullRequest => pullRequest.Id).ValueGeneratedNever();
        builder.Property(pullRequest => pullRequest.RepositoryFullName)
            .HasMaxLength(GitHubPullRequest.RepositoryNameMaximumLength)
            .IsRequired();
        builder.Property(pullRequest => pullRequest.Title)
            .HasMaxLength(GitHubPullRequest.TitleMaximumLength)
            .IsRequired();
        builder.Property(pullRequest => pullRequest.Url)
            .HasMaxLength(GitHubPullRequest.UrlMaximumLength)
            .IsRequired();
        builder.Property(pullRequest => pullRequest.AuthorLogin)
            .HasMaxLength(GitHubPullRequest.LoginMaximumLength);
        builder.Property(pullRequest => pullRequest.RequestedReviewers)
            .HasMaxLength(GitHubPullRequest.RequestedReviewersMaximumLength);
        builder.Property(pullRequest => pullRequest.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(pullRequest => pullRequest.CheckStatus)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(pullRequest => pullRequest.OpenedAtUtc).HasPrecision(7);
        builder.Property(pullRequest => pullRequest.LastActivityAtUtc).HasPrecision(7);

        builder.HasOne(pullRequest => pullRequest.Project)
            .WithMany()
            .HasForeignKey(pullRequest => new
            {
                pullRequest.WorkspaceId,
                pullRequest.ProjectId,
            })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pullRequest => new
        {
            pullRequest.WorkspaceId,
            pullRequest.ProjectId,
            pullRequest.RepositoryFullName,
            pullRequest.Number,
        }).IsUnique();
        builder.HasIndex(pullRequest => new
        {
            pullRequest.WorkspaceId,
            pullRequest.ProjectId,
            pullRequest.Status,
            pullRequest.LastActivityAtUtc,
        });
        builder.ConfigureAuditProperties();
    }
}
