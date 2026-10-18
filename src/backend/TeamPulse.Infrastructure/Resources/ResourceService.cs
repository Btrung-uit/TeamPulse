using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Resources;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Resources;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Resources;

internal sealed class ResourceService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : IResourceService
{
    public async Task<ResourceResult<IReadOnlyList<ResourceSummary>>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        string? search,
        ResourceType? type,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(workspaceId, projectId, cancellationToken))
        {
            return ResourceResult<IReadOnlyList<ResourceSummary>>.Failed(
                ResourceFailure.ProjectNotFound);
        }

        var normalizedSearch = search?.Trim().ToLowerInvariant();
        var query = dbContext.ProjectResources
            .AsNoTracking()
            .Where(resource =>
                resource.WorkspaceId == workspaceId &&
                resource.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(resource =>
                resource.Title.ToLower().Contains(normalizedSearch) ||
                (resource.ExternalId != null &&
                 resource.ExternalId.ToLower().Contains(normalizedSearch)));
        }

        if (type.HasValue)
        {
            query = query.Where(resource => resource.Type == type.Value);
        }

        var resources = await query
            .Include(resource => resource.OwnerMember.User)
            .Include(resource => resource.LinkedTask)
            .OrderByDescending(resource => resource.UpdatedAtUtc)
            .ThenBy(resource => resource.Title)
            .ToListAsync(cancellationToken);

        return ResourceResult<IReadOnlyList<ResourceSummary>>.Success(
            resources.Select(ToSummary).ToArray());
    }

    public async Task<ResourceResult<ResourceSummary>> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        SaveResourceCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateReferencesAsync(
            workspaceId,
            projectId,
            command.OwnerUserId,
            command.LinkedTaskId,
            cancellationToken);
        if (validation != ResourceFailure.None)
        {
            return ResourceResult<ResourceSummary>.Failed(validation);
        }

        var resource = new ProjectResource(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            command.OwnerUserId,
            command.Title,
            command.Type,
            command.Url,
            command.ExternalId,
            command.LinkedTaskId);
        dbContext.ProjectResources.Add(resource);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.ResourceCreated,
            resource.Id,
            newValue: resource.Title);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ResourceResult<ResourceSummary>.Success(
            (await FindSummaryAsync(workspaceId, projectId, resource.Id, cancellationToken))!);
    }

    public async Task<ResourceResult<ResourceSummary>> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        Guid actorUserId,
        SaveResourceCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateReferencesAsync(
            workspaceId,
            projectId,
            command.OwnerUserId,
            command.LinkedTaskId,
            cancellationToken);
        if (validation != ResourceFailure.None)
        {
            return ResourceResult<ResourceSummary>.Failed(validation);
        }

        var resource = await dbContext.ProjectResources.SingleOrDefaultAsync(
            item =>
                item.WorkspaceId == workspaceId &&
                item.ProjectId == projectId &&
                item.Id == resourceId,
            cancellationToken);
        if (resource is null)
        {
            return ResourceResult<ResourceSummary>.Failed(ResourceFailure.ResourceNotFound);
        }

        var previousTitle = resource.Title;
        resource.Update(
            command.Title,
            command.Type,
            command.Url,
            command.ExternalId,
            command.OwnerUserId,
            command.LinkedTaskId);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.ResourceUpdated,
            resource.Id,
            previousTitle,
            resource.Title);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ResourceResult<ResourceSummary>.Success(
            (await FindSummaryAsync(workspaceId, projectId, resource.Id, cancellationToken))!);
    }

    public async Task<ResourceResult<bool>> DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(workspaceId, projectId, cancellationToken))
        {
            return ResourceResult<bool>.Failed(ResourceFailure.ProjectNotFound);
        }

        var resource = await dbContext.ProjectResources.SingleOrDefaultAsync(
            item =>
                item.WorkspaceId == workspaceId &&
                item.ProjectId == projectId &&
                item.Id == resourceId,
            cancellationToken);
        if (resource is null)
        {
            return ResourceResult<bool>.Failed(ResourceFailure.ResourceNotFound);
        }

        dbContext.ProjectResources.Remove(resource);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.ResourceDeleted,
            resource.Id,
            previousValue: resource.Title);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ResourceResult<bool>.Success(true);
    }

    private async Task<ResourceFailure> ValidateReferencesAsync(
        Guid workspaceId,
        Guid projectId,
        Guid ownerUserId,
        Guid? linkedTaskId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.WorkspaceId == workspaceId && item.Id == projectId,
                cancellationToken);
        if (project is null)
        {
            return ResourceFailure.ProjectNotFound;
        }

        if (project.IsArchived)
        {
            return ResourceFailure.ProjectArchived;
        }

        if (!await dbContext.WorkspaceMembers.AnyAsync(
            member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == ownerUserId &&
                member.IsActive,
            cancellationToken))
        {
            return ResourceFailure.OwnerNotFound;
        }

        if (linkedTaskId.HasValue && !await dbContext.Tasks.AnyAsync(
            task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId &&
                task.Id == linkedTaskId.Value,
            cancellationToken))
        {
            return ResourceFailure.LinkedTaskNotFound;
        }

        return ResourceFailure.None;
    }

    private Task<bool> ProjectExistsAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken);

    private async Task<ResourceSummary?> FindSummaryAsync(
        Guid workspaceId,
        Guid projectId,
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var resource = await dbContext.ProjectResources
            .AsNoTracking()
            .Include(item => item.OwnerMember.User)
            .Include(item => item.LinkedTask)
            .SingleOrDefaultAsync(
                item =>
                    item.WorkspaceId == workspaceId &&
                    item.ProjectId == projectId &&
                    item.Id == resourceId,
                cancellationToken);
        return resource is null ? null : ToSummary(resource);
    }

    private static ResourceSummary ToSummary(ProjectResource resource) =>
        new(
            resource.Id,
            resource.WorkspaceId,
            resource.ProjectId,
            resource.Title,
            resource.Type,
            resource.Url,
            resource.ExternalId,
            new ResourceOwnerSummary(
                resource.OwnerUserId,
                resource.OwnerMember.User.DisplayName,
                resource.OwnerMember.User.Email),
            resource.LinkedTask is null
                ? null
                : new ResourceTaskSummary(
                    resource.LinkedTask.Id,
                    resource.LinkedTask.Code.Value,
                    resource.LinkedTask.Title),
            resource.CreatedAtUtc,
            resource.UpdatedAtUtc);

    private void AddActivity(
        Guid workspaceId,
        Guid actorUserId,
        ActivityAction action,
        Guid resourceId,
        string? previousValue = null,
        string? newValue = null)
    {
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            action,
            nameof(ProjectResource),
            resourceId,
            timeProvider.GetUtcNow(),
            previousValue: previousValue,
            newValue: newValue));
    }
}
