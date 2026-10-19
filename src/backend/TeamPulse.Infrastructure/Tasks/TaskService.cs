using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Tasks;

internal sealed class TaskService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : ITaskService
{
    public async Task<TaskResult<PagedTaskResult>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        TaskListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(workspaceId, projectId, cancellationToken))
        {
            return TaskResult<PagedTaskResult>.Failed(TaskFailure.ProjectNotFound);
        }

        var tasks = dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId);

        if (!query.IncludeArchived)
        {
            tasks = tasks.Where(task => !task.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            if (TryParseTaskCode(search, out var code))
            {
                tasks = tasks.Where(task => task.Code == code);
            }
            else
            {
                tasks = tasks.Where(task => task.Title.Contains(search));
            }
        }

        if (query.Status.HasValue)
        {
            tasks = tasks.Where(task => task.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            tasks = tasks.Where(task => task.Priority == query.Priority.Value);
        }

        if (query.AssigneeUserId.HasValue)
        {
            tasks = tasks.Where(task => task.AssigneeUserId == query.AssigneeUserId.Value);
        }

        if (query.SprintId.HasValue)
        {
            tasks = tasks.Where(task => task.SprintId == query.SprintId.Value);
        }

        if (query.DueBefore.HasValue)
        {
            tasks = tasks.Where(task =>
                task.DueOn.HasValue &&
                task.DueOn.Value <= query.DueBefore.Value);
        }

        var totalCount = await tasks.CountAsync(cancellationToken);
        List<ProjectTask> entities;
        var responseQuery = AddResponseIncludes(tasks);
        if (dbContext.Database.IsInMemory())
        {
            // EF InMemory cannot compare the TaskCode value object when two tasks
            // reach the final sort key. Materialize first and sort its scalar value.
            entities = (await responseQuery.ToListAsync(cancellationToken))
                .OrderBy(task => task.IsArchived)
                .ThenBy(task => task.DueOn == null)
                .ThenBy(task => task.DueOn)
                .ThenBy(task => task.Code.Value, StringComparer.Ordinal)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
        }
        else
        {
            entities = await responseQuery
                .OrderBy(task => task.IsArchived)
                .ThenBy(task => task.DueOn == null)
                .ThenBy(task => task.DueOn)
                .ThenBy(task => task.Code)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
        }
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return TaskResult<PagedTaskResult>.Success(new PagedTaskResult(
            entities.Select(ToListItem).ToArray(),
            query.Page,
            query.PageSize,
            totalCount,
            totalPages));
    }

    public async Task<TaskResult<TaskDetails>> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(workspaceId, projectId, cancellationToken))
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.ProjectNotFound);
        }

        var task = await LoadTaskAsync(
            workspaceId,
            projectId,
            taskId,
            asTracking: false,
            cancellationToken);
        return task is null
            ? TaskResult<TaskDetails>.Failed(TaskFailure.TaskNotFound)
            : TaskResult<TaskDetails>.Success(ToDetails(task));
    }

    public async Task<TaskResult<TaskDetails>> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        CreateTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(projectFailure);
        }

        if (await dbContext.Tasks.AnyAsync(
            task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId &&
                task.Code == command.Code,
            cancellationToken))
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskCodeConflict);
        }

        var relationshipFailure = await ValidateRelationshipsAsync(
            workspaceId,
            projectId,
            command.AssigneeUserId,
            command.ReviewerUserId,
            command.SprintId,
            cancellationToken);
        if (relationshipFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(relationshipFailure);
        }

        var task = new ProjectTask(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            actorUserId,
            command.Code,
            command.Title,
            command.Description,
            command.Priority);
        task.AssignTo(command.AssigneeUserId);
        task.SetReviewer(command.ReviewerUserId);
        task.AssignToSprint(command.SprintId);
        task.Schedule(command.DueOn, command.EstimateHours);
        dbContext.Tasks.Add(task);

        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.TaskCreated,
            task.Id,
            newValue: task.Code.Value);
        if (task.AssigneeUserId.HasValue)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskAssigned,
                task.Id,
                newValue: task.AssigneeUserId.Value.ToString());
        }

        if (task.DueOn.HasValue)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskDeadlineChanged,
                task.Id,
                newValue: FormatDate(task.DueOn));
        }

        if (task.SprintId.HasValue)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskSprintChanged,
                task.Id,
                newValue: task.SprintId.Value.ToString());
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskCodeConflict);
        }

        return await ReloadDetailsAsync(workspaceId, projectId, task.Id, cancellationToken);
    }

    public async Task<TaskResult<TaskDetails>> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        UpdateTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(projectFailure);
        }

        var task = await FindTaskAsync(workspaceId, projectId, taskId, cancellationToken);
        if (task is null)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskNotFound);
        }

        if (task.IsArchived)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskArchived);
        }

        if (task.Code != command.Code)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskCodeImmutable);
        }

        if (!RowVersionMatches(task, command.ExpectedRowVersion))
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.ConcurrencyConflict);
        }

        var relationshipFailure = await ValidateRelationshipsAsync(
            workspaceId,
            projectId,
            command.AssigneeUserId,
            command.ReviewerUserId,
            command.SprintId,
            cancellationToken);
        if (relationshipFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(relationshipFailure);
        }

        var previousAssignee = task.AssigneeUserId;
        var previousDueOn = task.DueOn;
        var previousSprint = task.SprintId;
        var detailsChanged =
            !string.Equals(task.Title, command.Title, StringComparison.Ordinal) ||
            !string.Equals(task.Description, command.Description, StringComparison.Ordinal) ||
            task.ReviewerUserId != command.ReviewerUserId ||
            task.Priority != command.Priority ||
            task.EstimateHours != command.EstimateHours;

        task.UpdateDetails(command.Title, command.Description);
        task.AssignTo(command.AssigneeUserId);
        task.SetReviewer(command.ReviewerUserId);
        task.AssignToSprint(command.SprintId);
        task.ChangePriority(command.Priority);
        task.Schedule(command.DueOn, command.EstimateHours);

        if (detailsChanged)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskUpdated,
                task.Id);
        }

        if (previousAssignee != task.AssigneeUserId)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskAssigned,
                task.Id,
                previousValue: previousAssignee?.ToString(),
                newValue: task.AssigneeUserId?.ToString());
        }

        if (previousDueOn != task.DueOn)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskDeadlineChanged,
                task.Id,
                previousValue: FormatDate(previousDueOn),
                newValue: FormatDate(task.DueOn));
        }

        if (previousSprint != task.SprintId)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskSprintChanged,
                task.Id,
                previousValue: previousSprint?.ToString(),
                newValue: task.SprintId?.ToString());
        }

        var saveFailure = await SaveWithConcurrencyAsync(cancellationToken);
        if (saveFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(saveFailure);
        }

        return await ReloadDetailsAsync(workspaceId, projectId, task.Id, cancellationToken);
    }

    public async Task<TaskResult<TaskDetails>> ChangeStatusAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        ChangeTaskStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(projectFailure);
        }

        var task = await FindTaskAsync(workspaceId, projectId, taskId, cancellationToken);
        if (task is null)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskNotFound);
        }

        if (task.IsArchived)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskArchived);
        }

        if (!RowVersionMatches(task, command.ExpectedRowVersion))
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.ConcurrencyConflict);
        }

        if (!await CanChangeStatusAsync(
            workspaceId,
            actorUserId,
            task,
            command.Status,
            cancellationToken))
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.Forbidden);
        }

        var previousStatus = task.Status;
        try
        {
            task.TransitionTo(command.Status);
        }
        catch (InvalidOperationException)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.InvalidStatusTransition);
        }

        if (previousStatus != task.Status)
        {
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskStatusChanged,
                task.Id,
                previousValue: previousStatus.ToString(),
                newValue: task.Status.ToString());
        }

        var saveFailure = await SaveWithConcurrencyAsync(cancellationToken);
        if (saveFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(saveFailure);
        }

        return await ReloadDetailsAsync(workspaceId, projectId, task.Id, cancellationToken);
    }

    public async Task<TaskResult<TaskDetails>> ArchiveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(projectFailure);
        }

        var task = await FindTaskAsync(workspaceId, projectId, taskId, cancellationToken);
        if (task is null)
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.TaskNotFound);
        }

        if (task.IsArchived)
        {
            return await ReloadDetailsAsync(workspaceId, projectId, task.Id, cancellationToken);
        }

        if (!RowVersionMatches(task, expectedRowVersion))
        {
            return TaskResult<TaskDetails>.Failed(TaskFailure.ConcurrencyConflict);
        }

        task.Archive(timeProvider.GetUtcNow());
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.TaskArchived,
            task.Id,
            previousValue: bool.FalseString,
            newValue: bool.TrueString);

        var saveFailure = await SaveWithConcurrencyAsync(cancellationToken);
        if (saveFailure != TaskFailure.None)
        {
            return TaskResult<TaskDetails>.Failed(saveFailure);
        }

        return await ReloadDetailsAsync(workspaceId, projectId, task.Id, cancellationToken);
    }

    private async Task<TaskFailure> ValidateRelationshipsAsync(
        Guid workspaceId,
        Guid projectId,
        Guid? assigneeUserId,
        Guid? reviewerUserId,
        Guid? sprintId,
        CancellationToken cancellationToken)
    {
        if (assigneeUserId.HasValue &&
            !await IsActiveMemberAsync(workspaceId, assigneeUserId.Value, cancellationToken))
        {
            return TaskFailure.AssigneeNotFound;
        }

        if (reviewerUserId.HasValue &&
            !await IsActiveMemberAsync(workspaceId, reviewerUserId.Value, cancellationToken))
        {
            return TaskFailure.ReviewerNotFound;
        }

        if (sprintId.HasValue &&
            !await dbContext.Sprints.AnyAsync(
                sprint =>
                    sprint.WorkspaceId == workspaceId &&
                    sprint.ProjectId == projectId &&
                    sprint.Id == sprintId.Value,
                cancellationToken))
        {
            return TaskFailure.SprintNotFound;
        }

        return TaskFailure.None;
    }

    private async Task<bool> CanChangeStatusAsync(
        Guid workspaceId,
        Guid actorUserId,
        ProjectTask task,
        ProjectTaskStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == actorUserId &&
                member.IsActive)
            .Select(member => (WorkspaceRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (role == WorkspaceRole.Leader)
        {
            return true;
        }

        if (role != WorkspaceRole.Member)
        {
            return false;
        }

        if (targetStatus == ProjectTaskStatus.Done)
        {
            return false;
        }

        if (targetStatus == ProjectTaskStatus.NeedsChanges)
        {
            return task.ReviewerUserId == actorUserId;
        }

        return task.AssigneeUserId == actorUserId;
    }

    private Task<bool> IsActiveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.WorkspaceMembers.AnyAsync(
            member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == userId &&
                member.IsActive,
            cancellationToken);

    private async Task<TaskFailure> GetProjectMutationFailureAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.WorkspaceId == workspaceId &&
                    candidate.Id == projectId,
                cancellationToken);

        if (project is null)
        {
            return TaskFailure.ProjectNotFound;
        }

        return project.IsArchived
            ? TaskFailure.ProjectArchived
            : TaskFailure.None;
    }

    private Task<bool> ProjectExistsAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken);

    private Task<ProjectTask?> FindTaskAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken) =>
        dbContext.Tasks.SingleOrDefaultAsync(
            task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId &&
                task.Id == taskId,
            cancellationToken);

    private async Task<ProjectTask?> LoadTaskAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<ProjectTask> query = dbContext.Tasks;
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await AddResponseIncludes(query)
            .SingleOrDefaultAsync(
                task =>
                    task.WorkspaceId == workspaceId &&
                    task.ProjectId == projectId &&
                    task.Id == taskId,
                cancellationToken);
    }

    private async Task<TaskResult<TaskDetails>> ReloadDetailsAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(
            workspaceId,
            projectId,
            taskId,
            asTracking: false,
            cancellationToken);
        return task is null
            ? TaskResult<TaskDetails>.Failed(TaskFailure.TaskNotFound)
            : TaskResult<TaskDetails>.Success(ToDetails(task));
    }

    private async Task<TaskFailure> SaveWithConcurrencyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return TaskFailure.None;
        }
        catch (DbUpdateConcurrencyException)
        {
            return TaskFailure.ConcurrencyConflict;
        }
    }

    private static IQueryable<ProjectTask> AddResponseIncludes(
        IQueryable<ProjectTask> query) =>
        query
            .Include(task => task.Sprint)
            .Include(task => task.AssigneeMember)
                .ThenInclude(member => member!.User)
            .Include(task => task.ReviewerMember)
                .ThenInclude(member => member!.User);

    private static bool RowVersionMatches(ProjectTask task, byte[] expectedRowVersion) =>
        task.RowVersion.AsSpan().SequenceEqual(expectedRowVersion);

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

    private static TaskListItem ToListItem(ProjectTask task) =>
        new(
            task.Id,
            task.Code.Value,
            task.Title,
            task.Status,
            task.Priority,
            task.DueOn,
            task.EstimateHours,
            ToMemberSummary(task.AssigneeMember),
            ToMemberSummary(task.ReviewerMember),
            ToSprintSummary(task.Sprint),
            task.IsArchived,
            task.UpdatedAtUtc,
            task.RowVersion.ToArray());

    private static TaskDetails ToDetails(ProjectTask task) =>
        new(
            task.Id,
            task.WorkspaceId,
            task.ProjectId,
            task.Code.Value,
            task.Title,
            task.Description,
            task.Status,
            task.StatusBeforeBlocked,
            task.Priority,
            task.DueOn,
            task.EstimateHours,
            ToMemberSummary(task.AssigneeMember),
            ToMemberSummary(task.ReviewerMember),
            ToSprintSummary(task.Sprint),
            task.IsArchived,
            task.ArchivedAtUtc,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.RowVersion.ToArray());

    private static TaskMemberSummary? ToMemberSummary(WorkspaceMember? member) =>
        member is null
            ? null
            : new TaskMemberSummary(
                member.UserId,
                member.User.DisplayName,
                member.User.Email);

    private static TaskSprintSummary? ToSprintSummary(Sprint? sprint) =>
        sprint is null ? null : new TaskSprintSummary(sprint.Id, sprint.Name);

    private static string? FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private void AddActivity(
        Guid workspaceId,
        Guid actorUserId,
        ActivityAction action,
        Guid taskId,
        string? previousValue = null,
        string? newValue = null)
    {
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            action,
            "Task",
            taskId,
            timeProvider.GetUtcNow(),
            previousValue: previousValue,
            newValue: newValue));
    }
}
