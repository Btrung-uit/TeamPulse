namespace TeamPulse.Domain.Activities;

public enum ActivityAction
{
    WorkspaceCreated,
    WorkspaceMemberAdded,
    WorkspaceMemberRoleChanged,
    ProjectCreated,
    ProjectUpdated,
    ProjectArchived,
    SprintCreated,
    SprintUpdated,
    SprintDeleted,
    TaskSprintChanged,
    TaskDependencyAdded,
    TaskDependencyRemoved,
    TaskCreated,
    TaskUpdated,
    TaskAssigned,
    TaskDeadlineChanged,
    TaskStatusChanged,
    TaskArchived,
}
