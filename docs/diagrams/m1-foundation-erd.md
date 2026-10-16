# Milestone 1 foundation ERD

This diagram documents the relational foundation introduced by `TP-010`.
`Workspace` is the tenant boundary; composite foreign keys carry that boundary
into project and sprint ownership.

```mermaid
erDiagram
    USER ||--o{ WORKSPACE : creates
    USER ||--o{ WORKSPACE_MEMBER : joins
    WORKSPACE ||--o{ WORKSPACE_MEMBER : contains
    WORKSPACE ||--o{ PROJECT : owns
    WORKSPACE_MEMBER ||--o{ PROJECT : creates
    PROJECT ||--o{ SPRINT : plans
    WORKSPACE_MEMBER ||--o{ SPRINT : creates

    USER {
        uniqueidentifier Id PK
        nvarchar Email
        nvarchar NormalizedEmail UK
        nvarchar DisplayName
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    WORKSPACE {
        uniqueidentifier Id PK
        uniqueidentifier CreatedByUserId FK
        nvarchar Name
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    WORKSPACE_MEMBER {
        uniqueidentifier WorkspaceId PK,FK
        uniqueidentifier UserId PK,FK
        nvarchar Role
        bit IsActive
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    PROJECT {
        uniqueidentifier Id PK
        uniqueidentifier WorkspaceId FK
        uniqueidentifier CreatedByUserId FK
        nvarchar Name
        nvarchar Description
        bit IsArchived
        datetimeoffset ArchivedAtUtc
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    SPRINT {
        uniqueidentifier Id PK
        uniqueidentifier WorkspaceId FK
        uniqueidentifier ProjectId FK
        uniqueidentifier CreatedByUserId FK
        nvarchar Name
        nvarchar Goal
        date StartsOn
        date EndsOn
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }
```

## Enforced invariants

- `WorkspaceMember` is unique by `(WorkspaceId, UserId)`.
- A `Project` creator must be a member of the same workspace through the
  `(WorkspaceId, CreatedByUserId)` foreign key.
- A `Sprint` must belong to a project in the same workspace through the
  `(WorkspaceId, ProjectId)` foreign key.
- A sprint cannot end before it starts.
- Project archive flag and archive timestamp must change together.
- Workspace-owned entities are protected by EF Core query filters. Code that
  intentionally crosses workspaces must explicitly opt out and perform its own
  authorization check.
