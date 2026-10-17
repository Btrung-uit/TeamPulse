namespace TeamPulse.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardResult<ProjectDashboard>> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
