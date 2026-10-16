using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Planning;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Planning;

internal sealed class PlanningService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : IPlanningService
{
    public async Task<PlanningResult<IReadOnlyList<SprintSummary>>> ListSprintsAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(workspaceId, projectId, cancellationToken))
        {
            return PlanningResult<IReadOnlyList<SprintSummary>>.Failed(
                PlanningFailure.ProjectNotFound);
        }

        var sprints = await dbContext.Sprints
            .AsNoTracking()
            .Where(sprint =>
                sprint.WorkspaceId == workspaceId &&
                sprint.ProjectId == projectId)
            .OrderBy(sprint => sprint.StartsOn)
            .ThenBy(sprint => sprint.Name)
            .Select(sprint => new SprintSummary(
                sprint.Id,
                sprint.WorkspaceId,
                sprint.ProjectId,
                sprint.Name,
                sprint.Goal,
                sprint.StartsOn,
                sprint.EndsOn,
                sprint.Tasks.Count,
                sprint.CreatedAtUtc,
                sprint.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return PlanningResult<IReadOnlyList<SprintSummary>>.Success(sprints);
    }

    public async Task<PlanningResult<SprintSummary>> CreateSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid actorUserId,
        SaveSprintCommand command,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != PlanningFailure.None)
        {
            return PlanningResult<SprintSummary>.Failed(projectFailure);
        }

        if (await SprintNameExistsAsync(
            workspaceId,
            projectId,
            command.Name,
            excludedSprintId: null,
            cancellationToken))
        {
            return PlanningResult<SprintSummary>.Failed(PlanningFailure.SprintNameConflict);
        }

        var sprint = new Sprint(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            actorUserId,
            command.Name,
            command.StartsOn,
            command.EndsOn,
            command.Goal);
        dbContext.Sprints.Add(sprint);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.SprintCreated,
            "Sprint",
            sprint.Id,
            newValue: sprint.Name);

        await dbContext.SaveChangesAsync(cancellationToken);
        return PlanningResult<SprintSummary>.Success(ToSprintSummary(sprint, taskCount: 0));
    }

    public async Task<PlanningResult<SprintSummary>> UpdateSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        Guid actorUserId,
        SaveSprintCommand command,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != PlanningFailure.None)
        {
            return PlanningResult<SprintSummary>.Failed(projectFailure);
        }

        var sprint = await FindSprintAsync(
            workspaceId,
            projectId,
            sprintId,
            cancellationToken);
        if (sprint is null)
        {
            return PlanningResult<SprintSummary>.Failed(PlanningFailure.SprintNotFound);
        }

        if (await SprintNameExistsAsync(
            workspaceId,
            projectId,
            command.Name,
            sprintId,
            cancellationToken))
        {
            return PlanningResult<SprintSummary>.Failed(PlanningFailure.SprintNameConflict);
        }

        var previousName = sprint.Name;
        sprint.UpdateDetails(command.Name, command.StartsOn, command.EndsOn, command.Goal);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.SprintUpdated,
            "Sprint",
            sprint.Id,
            previousValue: previousName,
            newValue: sprint.Name);

        await dbContext.SaveChangesAsync(cancellationToken);
        var taskCount = await dbContext.Tasks.CountAsync(
            task => task.SprintId == sprint.Id,
            cancellationToken);
        return PlanningResult<SprintSummary>.Success(ToSprintSummary(sprint, taskCount));
    }

    public async Task<PlanningResult<bool>> DeleteSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != PlanningFailure.None)
        {
            return PlanningResult<bool>.Failed(projectFailure);
        }

        var sprint = await FindSprintAsync(
            workspaceId,
            projectId,
            sprintId,
            cancellationToken);
        if (sprint is null)
        {
            return PlanningResult<bool>.Failed(PlanningFailure.SprintNotFound);
        }

        if (await dbContext.Tasks.AnyAsync(
            task => task.SprintId == sprint.Id,
            cancellationToken))
        {
            return PlanningResult<bool>.Failed(PlanningFailure.SprintHasTasks);
        }

        dbContext.Sprints.Remove(sprint);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.SprintDeleted,
            "Sprint",
            sprint.Id,
            previousValue: sprint.Name);
        await dbContext.SaveChangesAsync(cancellationToken);

        return PlanningResult<bool>.Success(true);
    }

    public async Task<PlanningResult<TaskSprintAssignmentSummary>> AssignTaskToSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid? sprintId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != PlanningFailure.None)
        {
            return PlanningResult<TaskSprintAssignmentSummary>.Failed(projectFailure);
        }

        var task = await FindTaskAsync(workspaceId, projectId, taskId, cancellationToken);
        if (task is null)
        {
            return PlanningResult<TaskSprintAssignmentSummary>.Failed(PlanningFailure.TaskNotFound);
        }

        if (task.IsArchived)
        {
            return PlanningResult<TaskSprintAssignmentSummary>.Failed(PlanningFailure.TaskArchived);
        }

        Sprint? sprint = null;
        if (sprintId.HasValue)
        {
            sprint = await FindSprintAsync(
                workspaceId,
                projectId,
                sprintId.Value,
                cancellationToken);
            if (sprint is null)
            {
                return PlanningResult<TaskSprintAssignmentSummary>.Failed(
                    PlanningFailure.SprintNotFound);
            }
        }

        if (task.SprintId != sprintId)
        {
            var previousSprintId = task.SprintId;
            task.AssignToSprint(sprintId);
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskSprintChanged,
                "Task",
                task.Id,
                previousValue: previousSprintId?.ToString(),
                newValue: sprintId?.ToString());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return PlanningResult<TaskSprintAssignmentSummary>.Success(
            new TaskSprintAssignmentSummary(
                task.Id,
                task.Code.Value,
                sprint?.Id,
                sprint?.Name));
    }

    public async Task<PlanningResult<IReadOnlyList<TaskDependencySummary>>> ListDependenciesAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(workspaceId, projectId, cancellationToken))
        {
            return PlanningResult<IReadOnlyList<TaskDependencySummary>>.Failed(
                PlanningFailure.ProjectNotFound);
        }

        var dependencies = await dbContext.TaskDependencies
            .AsNoTracking()
            .Where(dependency =>
                dependency.WorkspaceId == workspaceId &&
                dependency.ProjectId == projectId)
            .Include(dependency => dependency.PredecessorTask)
            .Include(dependency => dependency.SuccessorTask)
            .ToListAsync(cancellationToken);
        var summaries = dependencies
            .Select(ToDependencySummary)
            .OrderBy(summary => summary.Successor.Code, StringComparer.Ordinal)
            .ThenBy(summary => summary.Predecessor.Code, StringComparer.Ordinal)
            .ToArray();

        return PlanningResult<IReadOnlyList<TaskDependencySummary>>.Success(summaries);
    }

    public async Task<PlanningResult<TaskDependencySummary>> CreateDependencyAsync(
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != PlanningFailure.None)
        {
            return PlanningResult<TaskDependencySummary>.Failed(projectFailure);
        }

        if (predecessorTaskId == successorTaskId)
        {
            return PlanningResult<TaskDependencySummary>.Failed(PlanningFailure.SelfDependency);
        }

        var tasks = await dbContext.Tasks
            .Where(task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId &&
                (task.Id == predecessorTaskId || task.Id == successorTaskId))
            .ToListAsync(cancellationToken);
        if (tasks.Count != 2)
        {
            return PlanningResult<TaskDependencySummary>.Failed(PlanningFailure.TaskNotFound);
        }

        var tasksById = tasks.ToDictionary(task => task.Id);
        var predecessor = tasksById[predecessorTaskId];
        var successor = tasksById[successorTaskId];
        if (predecessor.IsArchived || successor.IsArchived)
        {
            return PlanningResult<TaskDependencySummary>.Failed(PlanningFailure.TaskArchived);
        }

        var existing = await dbContext.TaskDependencies.SingleOrDefaultAsync(
            dependency =>
                dependency.WorkspaceId == workspaceId &&
                dependency.ProjectId == projectId &&
                dependency.PredecessorTaskId == predecessorTaskId &&
                dependency.SuccessorTaskId == successorTaskId,
            cancellationToken);
        if (existing is not null)
        {
            return PlanningResult<TaskDependencySummary>.Success(
                ToDependencySummary(existing, predecessor, successor));
        }

        var links = await dbContext.TaskDependencies
            .Where(dependency =>
                dependency.WorkspaceId == workspaceId &&
                dependency.ProjectId == projectId)
            .Select(dependency => new TaskDependencyLink(
                dependency.PredecessorTaskId,
                dependency.SuccessorTaskId))
            .ToListAsync(cancellationToken);
        if (TaskDependencyGraph.WouldCreateCycle(
            links,
            predecessorTaskId,
            successorTaskId))
        {
            return PlanningResult<TaskDependencySummary>.Failed(PlanningFailure.DependencyCycle);
        }

        var dependency = new TaskDependency(
            workspaceId,
            projectId,
            predecessorTaskId,
            successorTaskId);
        dbContext.TaskDependencies.Add(dependency);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.TaskDependencyAdded,
            "TaskDependency",
            successorTaskId,
            newValue: predecessorTaskId.ToString());
        await dbContext.SaveChangesAsync(cancellationToken);

        return PlanningResult<TaskDependencySummary>.Success(
            ToDependencySummary(dependency, predecessor, successor));
    }

    public async Task<PlanningResult<bool>> DeleteDependencyAsync(
        Guid workspaceId,
        Guid projectId,
        Guid predecessorTaskId,
        Guid successorTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var projectFailure = await GetProjectMutationFailureAsync(
            workspaceId,
            projectId,
            cancellationToken);
        if (projectFailure != PlanningFailure.None)
        {
            return PlanningResult<bool>.Failed(projectFailure);
        }

        var taskCount = await dbContext.Tasks.CountAsync(
            task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId &&
                (task.Id == predecessorTaskId || task.Id == successorTaskId),
            cancellationToken);
        if (predecessorTaskId == successorTaskId || taskCount != 2)
        {
            return PlanningResult<bool>.Failed(PlanningFailure.TaskNotFound);
        }

        var dependency = await dbContext.TaskDependencies.SingleOrDefaultAsync(
            item =>
                item.WorkspaceId == workspaceId &&
                item.ProjectId == projectId &&
                item.PredecessorTaskId == predecessorTaskId &&
                item.SuccessorTaskId == successorTaskId,
            cancellationToken);
        if (dependency is null)
        {
            return PlanningResult<bool>.Success(true);
        }

        dbContext.TaskDependencies.Remove(dependency);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.TaskDependencyRemoved,
            "TaskDependency",
            successorTaskId,
            previousValue: predecessorTaskId.ToString());
        await dbContext.SaveChangesAsync(cancellationToken);

        return PlanningResult<bool>.Success(true);
    }

    private async Task<PlanningFailure> GetProjectMutationFailureAsync(
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
            return PlanningFailure.ProjectNotFound;
        }

        return project.IsArchived
            ? PlanningFailure.ProjectArchived
            : PlanningFailure.None;
    }

    private Task<bool> ProjectExistsAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken);

    private Task<Sprint?> FindSprintAsync(
        Guid workspaceId,
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        dbContext.Sprints.SingleOrDefaultAsync(
            sprint =>
                sprint.WorkspaceId == workspaceId &&
                sprint.ProjectId == projectId &&
                sprint.Id == sprintId,
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

    private Task<bool> SprintNameExistsAsync(
        Guid workspaceId,
        Guid projectId,
        string name,
        Guid? excludedSprintId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpper();
        return dbContext.Sprints.AnyAsync(
            sprint =>
                sprint.WorkspaceId == workspaceId &&
                sprint.ProjectId == projectId &&
                (!excludedSprintId.HasValue || sprint.Id != excludedSprintId.Value) &&
                sprint.Name.ToUpper() == normalizedName,
            cancellationToken);
    }

    private TaskDependencySummary ToDependencySummary(TaskDependency dependency)
    {
        return ToDependencySummary(
            dependency,
            dependency.PredecessorTask,
            dependency.SuccessorTask);
    }

    private TaskDependencySummary ToDependencySummary(
        TaskDependency dependency,
        ProjectTask predecessor,
        ProjectTask successor)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var risks = TaskDependencyRiskEvaluator.Evaluate(predecessor, successor, today)
            .Select(ToRiskSummary)
            .ToArray();

        return new TaskDependencySummary(
            ToDependencyTaskSummary(predecessor),
            ToDependencyTaskSummary(successor),
            risks.Length > 0,
            risks,
            dependency.CreatedAtUtc);
    }

    private static DependencyTaskSummary ToDependencyTaskSummary(ProjectTask task) =>
        new(
            task.Id,
            task.Code.Value,
            task.Title,
            task.Status,
            task.DueOn,
            task.IsArchived);

    private static DependencyRiskSummary ToRiskSummary(DependencyRiskKind kind) => kind switch
    {
        DependencyRiskKind.PredecessorIncomplete => new(
            kind,
            "predecessor_incomplete",
            "The predecessor is not complete yet."),
        DependencyRiskKind.PredecessorOverdue => new(
            kind,
            "predecessor_overdue",
            "The predecessor is overdue and may delay this task."),
        DependencyRiskKind.DeadlineConflict => new(
            kind,
            "deadline_conflict",
            "The predecessor is due after the successor."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown dependency risk."),
    };

    private static SprintSummary ToSprintSummary(Sprint sprint, int taskCount) =>
        new(
            sprint.Id,
            sprint.WorkspaceId,
            sprint.ProjectId,
            sprint.Name,
            sprint.Goal,
            sprint.StartsOn,
            sprint.EndsOn,
            taskCount,
            sprint.CreatedAtUtc,
            sprint.UpdatedAtUtc);

    private void AddActivity(
        Guid workspaceId,
        Guid actorUserId,
        ActivityAction action,
        string entityType,
        Guid entityId,
        string? previousValue = null,
        string? newValue = null)
    {
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            action,
            entityType,
            entityId,
            timeProvider.GetUtcNow(),
            previousValue: previousValue,
            newValue: newValue));
    }
}
