# Backend boundaries

- `TeamPulse.Api`: HTTP endpoints, authentication entry points, middleware, and composition root.
- `TeamPulse.Application`: use cases, commands/queries, DTOs, validation, and authorization policies.
- `TeamPulse.Domain`: domain entities, value objects, lifecycle rules, and domain events without infrastructure dependencies.
- `TeamPulse.Infrastructure`: EF Core, SQL Server, identity persistence, file import, GitHub clients, and external adapters.

These are boundaries inside one deployable modular monolith, not separate services.

## Dependency direction

```text
Api → Application → Domain
  └→ Infrastructure → Application + Domain
```

`Domain` has no project dependency. `Api` is the composition root and the only executable backend project.

## Commands

Run from the repository root:

```powershell
dotnet restore TeamPulse.sln
dotnet tool restore
dotnet build TeamPulse.sln --no-restore
dotnet test TeamPulse.sln --no-build
dotnet tool run dotnet-ef database update `
  --project src/backend/TeamPulse.Infrastructure `
  --startup-project src/backend/TeamPulse.Api
dotnet run --project src/backend/TeamPulse.Api
```

The development API listens at `http://localhost:5249`.

## Local configuration

The API requires the `ConnectionStrings:TeamPulse` setting and fails fast when it is missing. Keep the local value outside Git with .NET User Secrets:

```powershell
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "ConnectionStrings:TeamPulse" `
  "Server=localhost,1433;Database=TeamPulse;User Id=sa;Password=<your-password>;TrustServerCertificate=True"

$jwtSigningKey = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "Authentication:Jwt:SigningKey" $jwtSigningKey
```

Use `/health` or `/health/live` for liveness and `/health/ready` for SQL Server readiness. `/api` returns basic API metadata.

`TeamPulseDbContext` contains the `User`, `Workspace`, `WorkspaceMember`, `Project`, `Sprint`, `ProjectTask`, `TaskDependency`, and immutable `ActivityLog` schema. Its migrations live under `TeamPulse.Infrastructure/Persistence/Migrations`.

Workspace-owned entity queries are denied until a scoped `WorkspaceScope` is selected. The `WorkspaceMember` and `WorkspaceLeader` policies load an active membership from the database before selecting that scope. Any future query that deliberately bypasses the filters must explicitly use `IgnoreQueryFilters()` and perform an equivalent authorization check.

## Authentication endpoints

- `POST /api/auth/register`: create the domain profile and Identity credential in one transaction.
- `POST /api/auth/login`: validate the password with lockout protection.
- `POST /api/auth/refresh`: rotate the HTTP-only refresh cookie and issue a new JWT.
- `POST /api/auth/logout`: revoke the refresh session and delete its cookie.
- `GET /api/auth/me`: return the current user; requires a valid Bearer JWT.

The access token is short-lived and returned in the JSON body. Refresh tokens are never persisted in plaintext; only their SHA-256 hashes are stored in `RefreshSessions`.

## Workspace endpoints

- `GET /api/workspaces`: list the current user's active memberships.
- `POST /api/workspaces`: create a workspace and its first Leader membership.
- `GET /api/workspaces/{workspaceId}`: read a workspace as an active member.
- `GET /api/workspaces/{workspaceId}/members`: list active members.
- `POST /api/workspaces/{workspaceId}/members`: add an existing registered user; Leader only.
- `PATCH /api/workspaces/{workspaceId}/members/{userId}/role`: change a role; Leader only.

Member mutations create immutable `ActivityLog` rows. The API prevents demoting the final active Leader so a workspace cannot lose all administrators.

## Project endpoints

- `GET /api/workspaces/{workspaceId}/projects`: list active projects visible to any active workspace member; use `?includeArchived=true` to include archived projects.
- `GET /api/workspaces/{workspaceId}/projects/{projectId}`: read one project in the authorized workspace.
- `POST /api/workspaces/{workspaceId}/projects`: create a project; Leader only.
- `PUT /api/workspaces/{workspaceId}/projects/{projectId}`: update name and description; Leader only.
- `POST /api/workspaces/{workspaceId}/projects/{projectId}/archive`: soft-archive a project; Leader only and idempotent.

Project create, update, and archive operations append immutable `ActivityLog` rows. Archived projects retain their history and cannot be updated. Project visibility is workspace-wide in the MVP; there is no separate project-membership model.

## Task domain

The CLR entity is named `ProjectTask` to avoid ambiguity with .NET's asynchronous `Task`; the product, database table, and API language remain **Task**. A normalized `TaskCode` such as `TP-123` is unique inside a project.

The lifecycle is enforced in the domain:

```text
Todo → InProgress → EvidenceLinked → InReview → Done
                                  ↘ NeedsChanges
active state ⇄ Blocked (returns only to the previous state)
```

Only `InReview` can transition to `Done`, so commit/evidence attachment cannot complete work. `RowVersion` enables optimistic concurrency, archive is separate from workflow status, and hard deletion is allowed only for an unused draft with no related history. Dependency rows reference both tasks through `(WorkspaceId, ProjectId, TaskId)`, preventing cross-workspace and cross-project links at the database boundary.

## Planning endpoints

All routes use `/api/workspaces/{workspaceId}/projects/{projectId}`. Active workspace members can read planning data; only Leaders can mutate it.

- `GET /sprints`: list project sprints and their task counts.
- `POST /sprints`: create a sprint.
- `PUT /sprints/{sprintId}`: update its name, goal, and date range.
- `DELETE /sprints/{sprintId}`: delete an empty sprint; assigned tasks must be removed first.
- `PUT /tasks/{taskId}/sprint`: assign a task to a same-project sprint, or send `null` to unassign it.
- `GET /dependencies`: list edges with predecessor/successor UI properties and current risk warnings.
- `POST /dependencies`: add an idempotent dependency after same-project and cycle checks.
- `DELETE /dependencies/{predecessorTaskId}/{successorTaskId}`: remove an edge idempotently.

Risk warnings use stable codes: `predecessor_incomplete`, `predecessor_overdue`, and `deadline_conflict`. They are calculated from current task state and dates; the service never moves a deadline automatically. Planning mutations append immutable activity rows.

## Task endpoints

All routes use `/api/workspaces/{workspaceId}/projects/{projectId}/tasks`. Any active workspace member can list and read tasks. Only Leaders can create, fully update, or archive them.

- `GET /`: paginated list; filters include `search`, `status`, `priority`, `assigneeUserId`, `sprintId`, `dueBefore`, and `includeArchived`.
- `GET /{taskId}`: task details, including assignee, reviewer, sprint, and `rowVersion`.
- `POST /`: create a task with a project-unique immutable code; Leader only.
- `PUT /{taskId}`: replace editable task fields; Leader only and requires the latest `rowVersion`.
- `PATCH /{taskId}/status`: perform a domain-approved lifecycle transition and requires the latest `rowVersion`.
- `POST /{taskId}/archive`: soft-archive idempotently; Leader only and requires `rowVersion` on the first archive.

An assigned Member can progress their task except to `Done`; the designated Reviewer can move an `InReview` task to `NeedsChanges`; only a Leader can complete a task. Advisors are read-only. A stale token returns `409 task_conflict`, and the client must reload before retrying. Create, assignment, deadline, sprint, details, status, and archive changes append immutable activity rows. See `docs/api/tasks.md` for the full contract.

## Tenant and identity integration coverage

`TenantIdentityBoundaryTests` protects the Milestone 1 security boundary:

- anonymous and malformed Bearer requests receive `401` on tenant endpoints;
- a Leader from Workspace A receives `403` when addressing Workspace B and cannot list, read, create, update, archive, or change membership there;
- a foreign `projectId` disguised under an authorized Workspace A route returns `404` and cannot be mutated;
- `Member` and `Advisor` can read projects but receive `403` for project and membership mutations;
- authorization reads the current database membership on every request, so role demotion and membership deactivation take effect without issuing a new JWT.
