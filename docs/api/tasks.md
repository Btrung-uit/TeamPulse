# Task API contract

Base route:

```text
/api/workspaces/{workspaceId}/projects/{projectId}/tasks
```

All endpoints require a Bearer access token and an active membership in the route workspace. A workspace the caller cannot access returns `403`; a project or task outside the authorized route context returns `404`.

## Permissions

| Operation | Leader | Assigned Member | Reviewer Member | Advisor |
| --- | ---: | ---: | ---: | ---: |
| List/detail | Yes | Yes | Yes | Yes |
| Create/full update/archive | Yes | No | No | No |
| Valid status transition except `Done` | Yes | Yes | Only when also assigned | No |
| `InReview` → `NeedsChanges` | Yes | Only when also reviewer | Yes | No |
| `InReview` → `Done` | Yes | No | No | No |

The domain state machine remains authoritative after the permission check.

## List and detail

```http
GET /?page=1&pageSize=20&search=TP-022&status=InProgress&priority=High&assigneeUserId={id}&sprintId={id}&dueBefore=2026-11-30&includeArchived=false
GET /{taskId}
```

`page` starts at `1`; `pageSize` accepts `1..100`. `search` matches an exact normalized task code when it has the `PREFIX-NUMBER` format, otherwise it searches titles. Archived tasks are excluded unless `includeArchived=true`.

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

## Create and update

```http
POST /
PUT /{taskId}
Content-Type: application/json
```

```json
{
  "code": "TP-022",
  "title": "Implement Task API",
  "description": "List, mutate and retain history",
  "assigneeUserId": null,
  "reviewerUserId": null,
  "sprintId": null,
  "priority": "High",
  "dueOn": "2026-11-15",
  "estimateHours": 8,
  "rowVersion": "AAAAAAAAB9E="
}
```

`rowVersion` is omitted on create and required on update. `code` is normalized when created and cannot be changed later. Assignee and reviewer must be active members of the same workspace; sprint must belong to the same project.

## Status and archive

```http
PATCH /{taskId}/status
Content-Type: application/json

{
  "status": "InProgress",
  "rowVersion": "AAAAAAAAB9E="
}
```

```http
POST /{taskId}/archive
Content-Type: application/json

{
  "rowVersion": "AAAAAAAAB9E="
}
```

Archive is idempotent and preserves task details plus all related history. An archived task remains readable but cannot be edited or transitioned.

## Stable errors

| Status | Code | Meaning |
| ---: | --- | --- |
| 400 | `validation_failed` | Request field validation failed |
| 400 | `assignee_not_found` / `reviewer_not_found` | User is not an active workspace member |
| 403 | `task_mutation_forbidden` | Role or task relationship does not allow the transition |
| 404 | `project_not_found` / `task_not_found` / `sprint_not_found` | Resource is outside the authorized route context |
| 409 | `task_code_conflict` | Code already exists in the project |
| 409 | `task_code_immutable` | Update attempted to change the code |
| 409 | `invalid_status_transition` | Domain lifecycle rejected the transition |
| 409 | `task_conflict` | `rowVersion` is stale; reload and retry |
| 409 | `task_archived` / `project_archived` | Archived aggregate cannot be mutated |

Errors follow the shared `{ code, message, errors? }` envelope.
