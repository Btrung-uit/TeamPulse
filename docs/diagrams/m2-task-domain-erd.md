# Milestone 2 task domain ERD

This diagram documents the task aggregate introduced by `TP-020`. `WorkspaceId`
and `ProjectId` are carried through composite keys so relational integrity, not
only application code, prevents tenant and project boundary violations.

```mermaid
erDiagram
    WORKSPACE ||--o{ PROJECT : owns
    PROJECT ||--o{ TASK : contains
    SPRINT o|--o{ TASK : schedules
    WORKSPACE_MEMBER ||--o{ TASK : creates
    WORKSPACE_MEMBER o|--o{ TASK : is_assigned
    WORKSPACE_MEMBER o|--o{ TASK : reviews
    TASK ||--o{ TASK_DEPENDENCY : predecessor
    TASK ||--o{ TASK_DEPENDENCY : successor

    TASK {
        uniqueidentifier Id PK
        uniqueidentifier WorkspaceId FK
        uniqueidentifier ProjectId FK
        uniqueidentifier SprintId FK
        uniqueidentifier CreatedByUserId FK
        uniqueidentifier AssigneeUserId FK
        uniqueidentifier ReviewerUserId FK
        varchar Code "UK per project"
        nvarchar Title
        nvarchar Description
        nvarchar Status
        nvarchar StatusBeforeBlocked
        nvarchar Priority
        date DueOn
        decimal EstimateHours
        bit HasStartedWorkflow
        bit IsArchived
        datetimeoffset ArchivedAtUtc
        rowversion RowVersion
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    TASK_DEPENDENCY {
        uniqueidentifier WorkspaceId PK,FK
        uniqueidentifier ProjectId PK,FK
        uniqueidentifier PredecessorTaskId PK,FK
        uniqueidentifier SuccessorTaskId PK,FK
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }
```

## Enforced invariants

- `(WorkspaceId, ProjectId, Code)` is unique.
- Creator, assignee, and reviewer reference membership in the same workspace.
- Sprint assignment references a sprint in the same workspace and project.
- Both dependency endpoints reference tasks in the same workspace and project.
- A dependency cannot point a task to itself; application-level graph traversal rejects direct and indirect cycles before insert.
- Status, priority, estimate, blocked-state, and archive-state check constraints
  protect persisted data in addition to domain validation.
- `RowVersion` is the optimistic concurrency token.
- Task and dependency queries are denied until the request selects a workspace.
