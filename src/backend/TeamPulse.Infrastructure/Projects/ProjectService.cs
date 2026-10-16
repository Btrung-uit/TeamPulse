using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Projects;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Projects;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Projects;

internal sealed class ProjectService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : IProjectService
{
    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId);

        if (!includeArchived)
        {
            query = query.Where(project => !project.IsArchived);
        }

        return await query
            .OrderBy(project => project.IsArchived)
            .ThenByDescending(project => project.UpdatedAtUtc)
            .Select(project => ToSummary(project))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectSummary?> GetAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId && project.Id == projectId)
            .Select(project => ToSummary(project))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProjectSummary> CreateAsync(
        Guid workspaceId,
        Guid actorUserId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var project = new Project(Guid.NewGuid(), workspaceId, actorUserId, name, description);
        dbContext.Projects.Add(project);
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            ActivityAction.ProjectCreated,
            "Project",
            project.Id,
            timeProvider.GetUtcNow(),
            newValue: project.Name));

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(project);
    }

    public async Task<ProjectResult<ProjectSummary>> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var project = await FindAsync(workspaceId, projectId, cancellationToken);
        if (project is null)
        {
            return ProjectResult<ProjectSummary>.Failed(ProjectFailure.ProjectNotFound);
        }

        if (project.IsArchived)
        {
            return ProjectResult<ProjectSummary>.Failed(ProjectFailure.ProjectArchived);
        }

        var previousName = project.Name;
        project.UpdateDetails(name, description);
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            ActivityAction.ProjectUpdated,
            "Project",
            project.Id,
            timeProvider.GetUtcNow(),
            previousValue: previousName,
            newValue: project.Name));

        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectResult<ProjectSummary>.Success(ToSummary(project));
    }

    public async Task<ProjectResult<ProjectSummary>> ArchiveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindAsync(workspaceId, projectId, cancellationToken);
        if (project is null)
        {
            return ProjectResult<ProjectSummary>.Failed(ProjectFailure.ProjectNotFound);
        }

        if (project.IsArchived)
        {
            return ProjectResult<ProjectSummary>.Success(ToSummary(project));
        }

        project.Archive(timeProvider.GetUtcNow());
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            ActivityAction.ProjectArchived,
            "Project",
            project.Id,
            timeProvider.GetUtcNow(),
            previousValue: bool.FalseString,
            newValue: bool.TrueString));

        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectResult<ProjectSummary>.Success(ToSummary(project));
    }

    private Task<Project?> FindAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        dbContext.Projects.SingleOrDefaultAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken);

    private static ProjectSummary ToSummary(Project project) =>
        new(
            project.Id,
            project.WorkspaceId,
            project.Name,
            project.Description,
            project.IsArchived,
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            project.ArchivedAtUtc);
}
