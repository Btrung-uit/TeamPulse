using TeamPulse.Domain.Tasks;

namespace TeamPulse.Domain.Integrations;

public sealed class GitHubPullRequestTask
{
    private GitHubPullRequestTask()
    {
    }

    public GitHubPullRequestTask(
        Guid pullRequestId,
        Guid workspaceId,
        Guid projectId,
        Guid taskId)
    {
        if (pullRequestId == Guid.Empty || workspaceId == Guid.Empty ||
            projectId == Guid.Empty || taskId == Guid.Empty)
        {
            throw new ArgumentException("Pull request task identifiers cannot be empty.");
        }

        PullRequestId = pullRequestId;
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        TaskId = taskId;
    }

    public Guid PullRequestId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TaskId { get; private set; }
    public GitHubPullRequest PullRequest { get; private set; } = null!;
    public ProjectTask Task { get; private set; } = null!;
}
