using TeamPulse.Domain.Common;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Domain.Tasks;

public sealed class ProjectTask : AuditableEntity
{
    public const int TitleMaximumLength = 240;
    public const int DescriptionMaximumLength = 5_000;
    public const decimal EstimateHoursMaximum = 9_999.99m;

    private ProjectTask()
    {
    }

    public ProjectTask(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        Guid createdByUserId,
        TaskCode code,
        string title,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium)
        : base(id)
    {
        EnsureRequiredId(workspaceId, nameof(workspaceId), "Task workspace is required.");
        EnsureRequiredId(projectId, nameof(projectId), "Task project is required.");
        EnsureRequiredId(createdByUserId, nameof(createdByUserId), "Task creator is required.");
        EnsureValidPriority(priority);

        if (string.IsNullOrWhiteSpace(code.Value))
        {
            throw new ArgumentException("Task code is required.", nameof(code));
        }

        WorkspaceId = workspaceId;
        ProjectId = projectId;
        CreatedByUserId = createdByUserId;
        Code = code;
        Title = DomainText.Required(title, nameof(title), TitleMaximumLength);
        Description = DomainText.Optional(description, nameof(description), DescriptionMaximumLength);
        Priority = priority;
        Status = ProjectTaskStatus.Todo;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? SprintId { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public Guid? ReviewerUserId { get; private set; }

    public TaskCode Code { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ProjectTaskStatus Status { get; private set; }

    public ProjectTaskStatus? StatusBeforeBlocked { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateOnly? DueOn { get; private set; }

    public decimal? EstimateHours { get; private set; }

    public bool HasStartedWorkflow { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public Project Project { get; private set; } = null!;

    public Sprint? Sprint { get; private set; }

    public WorkspaceMember CreatedByMember { get; private set; } = null!;

    public WorkspaceMember? AssigneeMember { get; private set; }

    public WorkspaceMember? ReviewerMember { get; private set; }

    public ICollection<TaskDependency> PredecessorDependencies { get; } = [];

    public ICollection<TaskDependency> SuccessorDependencies { get; } = [];

    public ICollection<TaskEvidence> Evidence { get; } = [];

    public ICollection<TaskComment> Comments { get; } = [];

    public void UpdateDetails(string title, string? description)
    {
        EnsureMutable();
        Title = DomainText.Required(title, nameof(title), TitleMaximumLength);
        Description = DomainText.Optional(description, nameof(description), DescriptionMaximumLength);
    }

    public void AssignTo(Guid? assigneeUserId)
    {
        EnsureMutable();
        EnsureOptionalId(assigneeUserId, nameof(assigneeUserId));
        AssigneeUserId = assigneeUserId;
    }

    public void SetReviewer(Guid? reviewerUserId)
    {
        EnsureMutable();
        EnsureOptionalId(reviewerUserId, nameof(reviewerUserId));
        ReviewerUserId = reviewerUserId;
    }

    public void AssignToSprint(Guid? sprintId)
    {
        EnsureMutable();
        EnsureOptionalId(sprintId, nameof(sprintId));
        SprintId = sprintId;
    }

    public void ChangePriority(TaskPriority priority)
    {
        EnsureMutable();
        EnsureValidPriority(priority);
        Priority = priority;
    }

    public void Schedule(DateOnly? dueOn, decimal? estimateHours)
    {
        EnsureMutable();

        if (estimateHours is <= 0 or > EstimateHoursMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimateHours),
                estimateHours,
                $"Estimate must be greater than zero and no more than {EstimateHoursMaximum} hours.");
        }

        DueOn = dueOn;
        EstimateHours = estimateHours;
    }

    public void TransitionTo(ProjectTaskStatus targetStatus)
    {
        EnsureMutable();
        EnsureValidStatus(targetStatus);

        if (targetStatus == Status)
        {
            return;
        }

        if (Status == ProjectTaskStatus.Blocked)
        {
            if (targetStatus != StatusBeforeBlocked)
            {
                throw InvalidTransition(Status, targetStatus);
            }

            Status = targetStatus;
            StatusBeforeBlocked = null;
            return;
        }

        if (targetStatus == ProjectTaskStatus.Blocked)
        {
            if (Status == ProjectTaskStatus.Done)
            {
                throw InvalidTransition(Status, targetStatus);
            }

            StatusBeforeBlocked = Status;
            Status = ProjectTaskStatus.Blocked;
            HasStartedWorkflow = true;
            return;
        }

        if (!IsAllowedTransition(Status, targetStatus))
        {
            throw InvalidTransition(Status, targetStatus);
        }

        Status = targetStatus;
        HasStartedWorkflow = HasStartedWorkflow || targetStatus != ProjectTaskStatus.Todo;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        ArchivedAtUtc = archivedAtUtc;
    }

    public bool CanBeHardDeleted(bool hasRelatedHistory)
    {
        return !hasRelatedHistory && !HasStartedWorkflow && !IsArchived;
    }

    private static bool IsAllowedTransition(
        ProjectTaskStatus currentStatus,
        ProjectTaskStatus targetStatus)
    {
        return currentStatus switch
        {
            ProjectTaskStatus.Todo => targetStatus == ProjectTaskStatus.InProgress,
            ProjectTaskStatus.InProgress => targetStatus is
                ProjectTaskStatus.Todo or ProjectTaskStatus.EvidenceLinked,
            ProjectTaskStatus.EvidenceLinked => targetStatus is
                ProjectTaskStatus.InProgress or ProjectTaskStatus.InReview,
            ProjectTaskStatus.InReview => targetStatus is
                ProjectTaskStatus.InProgress or ProjectTaskStatus.NeedsChanges or ProjectTaskStatus.Done,
            ProjectTaskStatus.NeedsChanges => targetStatus is
                ProjectTaskStatus.InProgress or ProjectTaskStatus.InReview,
            ProjectTaskStatus.Done => false,
            ProjectTaskStatus.Blocked => false,
            _ => false,
        };
    }

    private void EnsureMutable()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived tasks cannot be changed.");
        }
    }

    private static void EnsureValidStatus(ProjectTaskStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status.");
        }
    }

    private static void EnsureValidPriority(TaskPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown task priority.");
        }
    }

    private static void EnsureRequiredId(Guid id, string parameterName, string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private static void EnsureOptionalId(Guid? id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static InvalidOperationException InvalidTransition(
        ProjectTaskStatus currentStatus,
        ProjectTaskStatus targetStatus)
    {
        return new InvalidOperationException(
            $"Task status cannot transition from {currentStatus} to {targetStatus}.");
    }
}
