using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Notifications;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Tasks;

internal sealed class TaskCollaborationService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : ITaskCollaborationService
{
    public async Task<TaskResult<TaskCollaborationDetails>> GetAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!await TaskExistsAsync(workspaceId, projectId, taskId, cancellationToken))
        {
            return TaskResult<TaskCollaborationDetails>.Failed(
                await ProjectExistsAsync(workspaceId, projectId, cancellationToken)
                    ? TaskFailure.TaskNotFound
                    : TaskFailure.ProjectNotFound);
        }

        var evidence = await dbContext.TaskEvidence
            .AsNoTracking()
            .Where(item =>
                item.WorkspaceId == workspaceId &&
                item.ProjectId == projectId &&
                item.TaskId == taskId)
            .Include(item => item.SubmittedByMember)
                .ThenInclude(member => member.User)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new TaskEvidenceItem(
                item.Id,
                item.Type,
                item.Url,
                item.Note,
                new TaskMemberSummary(
                    item.SubmittedByMember.UserId,
                    item.SubmittedByMember.User.DisplayName,
                    item.SubmittedByMember.User.Email),
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var comments = await dbContext.TaskComments
            .AsNoTracking()
            .Where(item =>
                item.WorkspaceId == workspaceId &&
                item.ProjectId == projectId &&
                item.TaskId == taskId)
            .Include(item => item.AuthorMember)
                .ThenInclude(member => member.User)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new TaskCommentItem(
                item.Id,
                item.Kind,
                item.Body,
                new TaskMemberSummary(
                    item.AuthorMember.UserId,
                    item.AuthorMember.User.DisplayName,
                    item.AuthorMember.User.Email),
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return TaskResult<TaskCollaborationDetails>.Success(new(evidence, comments));
    }

    public async Task<TaskResult<TaskEvidenceItem>> AddEvidenceAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        AddTaskEvidenceCommand command,
        CancellationToken cancellationToken = default)
    {
        var task = await LoadMutableTaskAsync(workspaceId, projectId, taskId, cancellationToken);
        if (task.Result is null)
        {
            return TaskResult<TaskEvidenceItem>.Failed(task.Failure);
        }

        var role = await GetRoleAsync(workspaceId, actorUserId, cancellationToken);
        if (role != WorkspaceRole.Leader &&
            (role != WorkspaceRole.Member || task.Result.AssigneeUserId != actorUserId))
        {
            return TaskResult<TaskEvidenceItem>.Failed(TaskFailure.Forbidden);
        }

        var evidence = new TaskEvidence(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            taskId,
            actorUserId,
            command.Type,
            command.Url,
            command.Note);
        dbContext.TaskEvidence.Add(evidence);

        if (task.Result.Status == ProjectTaskStatus.InProgress)
        {
            task.Result.TransitionTo(ProjectTaskStatus.EvidenceLinked);
            AddActivity(
                workspaceId,
                actorUserId,
                ActivityAction.TaskStatusChanged,
                taskId,
                ProjectTaskStatus.InProgress.ToString(),
                ProjectTaskStatus.EvidenceLinked.ToString());
        }

        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.TaskEvidenceAdded,
            taskId,
            newValue: command.Type.ToString());

        var failure = await SaveAsync(cancellationToken);
        if (failure != TaskFailure.None)
        {
            return TaskResult<TaskEvidenceItem>.Failed(failure);
        }

        var created = await dbContext.TaskEvidence
            .AsNoTracking()
            .Include(item => item.SubmittedByMember)
                .ThenInclude(member => member.User)
            .SingleAsync(item => item.Id == evidence.Id, cancellationToken);
        return TaskResult<TaskEvidenceItem>.Success(ToEvidenceItem(created));
    }

    public async Task<TaskResult<TaskReviewResult>> ReviewAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        Guid actorUserId,
        ReviewTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        var task = await LoadMutableTaskAsync(workspaceId, projectId, taskId, cancellationToken);
        if (task.Result is null)
        {
            return TaskResult<TaskReviewResult>.Failed(task.Failure);
        }

        if (await GetRoleAsync(workspaceId, actorUserId, cancellationToken) != WorkspaceRole.Leader)
        {
            return TaskResult<TaskReviewResult>.Failed(TaskFailure.Forbidden);
        }

        if (!task.Result.RowVersion.AsSpan().SequenceEqual(command.ExpectedRowVersion))
        {
            return TaskResult<TaskReviewResult>.Failed(TaskFailure.ConcurrencyConflict);
        }

        if (task.Result.Status != ProjectTaskStatus.InReview)
        {
            return TaskResult<TaskReviewResult>.Failed(TaskFailure.ReviewRequiresInReview);
        }

        var target = command.Decision == TaskReviewDecision.Accept
            ? ProjectTaskStatus.Done
            : ProjectTaskStatus.NeedsChanges;
        var kind = command.Decision == TaskReviewDecision.Accept
            ? TaskCommentKind.ReviewAccepted
            : TaskCommentKind.ChangesRequested;
        var action = command.Decision == TaskReviewDecision.Accept
            ? ActivityAction.TaskReviewAccepted
            : ActivityAction.TaskChangesRequested;

        task.Result.TransitionTo(target);
        var comment = new TaskComment(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            taskId,
            actorUserId,
            kind,
            command.Comment);
        dbContext.TaskComments.Add(comment);
        AddActivity(
            workspaceId,
            actorUserId,
            ActivityAction.TaskStatusChanged,
            taskId,
            ProjectTaskStatus.InReview.ToString(),
            target.ToString());
        AddActivity(workspaceId, actorUserId, action, taskId);
        if (task.Result.AssigneeUserId.HasValue &&
            task.Result.AssigneeUserId.Value != actorUserId)
        {
            var accepted = command.Decision == TaskReviewDecision.Accept;
            dbContext.Notifications.Add(new Notification(
                Guid.NewGuid(),
                workspaceId,
                task.Result.AssigneeUserId.Value,
                accepted
                    ? NotificationKind.ReviewAccepted
                    : NotificationKind.ChangesRequested,
                accepted
                    ? $"Review accepted: {task.Result.Code.Value}"
                    : $"Changes requested: {task.Result.Code.Value}",
                accepted
                    ? $"\"{task.Result.Title}\" was accepted."
                    : $"\"{task.Result.Title}\" needs more changes.",
                projectId,
                taskId));
        }

        var failure = await SaveAsync(cancellationToken);
        if (failure != TaskFailure.None)
        {
            return TaskResult<TaskReviewResult>.Failed(failure);
        }

        var created = await dbContext.TaskComments
            .AsNoTracking()
            .Include(item => item.AuthorMember)
                .ThenInclude(member => member.User)
            .SingleAsync(item => item.Id == comment.Id, cancellationToken);
        return TaskResult<TaskReviewResult>.Success(new(
            taskId,
            task.Result.Status,
            task.Result.RowVersion.ToArray(),
            ToCommentItem(created)));
    }

    private async Task<(ProjectTask? Result, TaskFailure Failure)> LoadMutableTaskAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.WorkspaceId == workspaceId && item.Id == projectId,
                cancellationToken);
        if (project is null)
        {
            return (null, TaskFailure.ProjectNotFound);
        }

        if (project.IsArchived)
        {
            return (null, TaskFailure.ProjectArchived);
        }

        var task = await dbContext.Tasks.SingleOrDefaultAsync(
            item =>
                item.WorkspaceId == workspaceId &&
                item.ProjectId == projectId &&
                item.Id == taskId,
            cancellationToken);
        if (task is null)
        {
            return (null, TaskFailure.TaskNotFound);
        }

        return task.IsArchived
            ? (null, TaskFailure.TaskArchived)
            : (task, TaskFailure.None);
    }

    private Task<bool> ProjectExistsAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken);

    private Task<bool> TaskExistsAsync(
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken) =>
        dbContext.Tasks.AnyAsync(
            task =>
                task.WorkspaceId == workspaceId &&
                task.ProjectId == projectId &&
                task.Id == taskId,
            cancellationToken);

    private Task<WorkspaceRole?> GetRoleAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == userId &&
                member.IsActive)
            .Select(member => (WorkspaceRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<TaskFailure> SaveAsync(CancellationToken cancellationToken)
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

    private void AddActivity(
        Guid workspaceId,
        Guid actorUserId,
        ActivityAction action,
        Guid taskId,
        string? previousValue = null,
        string? newValue = null) =>
        dbContext.ActivityLogs.Add(new ActivityLog(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            action,
            nameof(ProjectTask),
            taskId,
            timeProvider.GetUtcNow(),
            previousValue: previousValue,
            newValue: newValue));

    private static TaskEvidenceItem ToEvidenceItem(TaskEvidence evidence) =>
        new(
            evidence.Id,
            evidence.Type,
            evidence.Url,
            evidence.Note,
            new TaskMemberSummary(
                evidence.SubmittedByMember.UserId,
                evidence.SubmittedByMember.User.DisplayName,
                evidence.SubmittedByMember.User.Email),
            evidence.CreatedAtUtc);

    private static TaskCommentItem ToCommentItem(TaskComment comment) =>
        new(
            comment.Id,
            comment.Kind,
            comment.Body,
            new TaskMemberSummary(
                comment.AuthorMember.UserId,
                comment.AuthorMember.User.DisplayName,
                comment.AuthorMember.User.Email),
            comment.CreatedAtUtc);
}
