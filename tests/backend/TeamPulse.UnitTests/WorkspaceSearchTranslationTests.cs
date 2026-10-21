using Microsoft.EntityFrameworkCore;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Multitenancy;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.UnitTests;

public sealed class WorkspaceSearchTranslationTests
{
    [Fact]
    public void TaskSearchQuery_CanBeTranslatedBySqlServer()
    {
        var workspaceId = Guid.NewGuid();
        var workspaceScope = new WorkspaceScope();
        workspaceScope.SetWorkspace(workspaceId);

        var options = new DbContextOptionsBuilder<TeamPulseDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=TranslationOnly;User Id=unused;Password=unused;" +
                "TrustServerCertificate=True")
            .Options;

        using var dbContext = new TeamPulseDbContext(
            options,
            workspaceScope,
            TimeProvider.System);

        var normalizedQuery = "tp-501";
        var taskCode = TaskCode.Parse(normalizedQuery);

        var query = dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.WorkspaceId == workspaceId &&
                !task.IsArchived &&
                !task.Project.IsArchived)
            .Where(task =>
                task.Code == taskCode ||
                task.Title.ToLower().Contains(normalizedQuery) ||
                (task.Description != null &&
                 task.Description.ToLower().Contains(normalizedQuery)))
            .OrderByDescending(task => task.UpdatedAtUtc)
            .Take(10)
            .Select(task => new
            {
                task.Id,
                task.ProjectId,
                ProjectName = task.Project.Name,
                task.Code,
                task.Title,
                task.Status,
            });

        var sql = query.ToQueryString();

        Assert.Contains("[t].[Code]", sql, StringComparison.Ordinal);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

}
