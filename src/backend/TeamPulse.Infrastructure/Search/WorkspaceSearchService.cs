using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Search;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Search;

internal sealed class WorkspaceSearchService(TeamPulseDbContext dbContext)
    : IWorkspaceSearchService
{
    public async Task<WorkspaceSearchResults> SearchAsync(
        Guid workspaceId,
        string query,
        int limitPerType,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = query.Trim();
        var normalizedQuery = searchQuery.ToLower();

        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.WorkspaceId == workspaceId &&
                !project.IsArchived &&
                (project.Name.ToLower().Contains(normalizedQuery) ||
                 (project.Description != null &&
                  project.Description.ToLower().Contains(normalizedQuery))))
            .OrderBy(project => project.Name)
            .Take(limitPerType)
            .Select(project => new ProjectSearchResult(
                project.Id,
                project.Name,
                project.Description))
            .ToListAsync(cancellationToken);

        var taskQuery = dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.WorkspaceId == workspaceId &&
                !task.IsArchived &&
                !task.Project.IsArchived);

        if (TryParseTaskCode(searchQuery, out var taskCode))
        {
            taskQuery = taskQuery.Where(task =>
                task.Code == taskCode ||
                task.Title.ToLower().Contains(normalizedQuery) ||
                (task.Description != null &&
                 task.Description.ToLower().Contains(normalizedQuery)));
        }
        else
        {
            taskQuery = taskQuery.Where(task =>
                task.Title.ToLower().Contains(normalizedQuery) ||
                (task.Description != null &&
                 task.Description.ToLower().Contains(normalizedQuery)));
        }

        var taskRows = await taskQuery
            .OrderByDescending(task => task.UpdatedAtUtc)
            .Take(limitPerType)
            .Select(task => new
            {
                task.Id,
                task.ProjectId,
                ProjectName = task.Project.Name,
                task.Code,
                task.Title,
                task.Status,
            })
            .ToListAsync(cancellationToken);

        var tasks = taskRows
            .Select(task => new TaskSearchResult(
                task.Id,
                task.ProjectId,
                task.ProjectName,
                task.Code.Value,
                task.Title,
                task.Status))
            .ToList();

        var members = await dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == workspaceId &&
                member.IsActive &&
                (member.User.DisplayName.ToLower().Contains(normalizedQuery) ||
                 member.User.Email.ToLower().Contains(normalizedQuery)))
            .OrderBy(member => member.User.DisplayName)
            .Take(limitPerType)
            .Select(member => new MemberSearchResult(
                member.UserId,
                member.User.DisplayName,
                member.User.Email,
                member.Role))
            .ToListAsync(cancellationToken);

        return new WorkspaceSearchResults(projects, tasks, members);
    }

    private static bool TryParseTaskCode(string value, out TaskCode code)
    {
        try
        {
            code = TaskCode.Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            code = default;
            return false;
        }
    }
}
