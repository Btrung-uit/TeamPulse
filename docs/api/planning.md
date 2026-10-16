# Planning API

Base route:

```text
/api/workspaces/{workspaceId}/projects/{projectId}
```

Every route requires an active workspace membership. Reads accept `Leader`,
`Member`, and `Advisor`; writes require `Leader`.

## Sprint routes

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/sprints` | Lists sprints ordered by start date with `taskCount`. |
| `POST` | `/sprints` | Creates a sprint with name, optional goal, start, and end. |
| `PUT` | `/sprints/{sprintId}` | Replaces sprint planning details. |
| `DELETE` | `/sprints/{sprintId}` | Deletes only when no task is assigned. |
| `PUT` | `/tasks/{taskId}/sprint` | Assigns a same-project sprint; `{"sprintId": null}` unassigns. |

Sprint name is unique per project. Date ranges require `endsOn >= startsOn`.

## Dependency routes

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/dependencies` | Returns all edges and computed risk properties. |
| `POST` | `/dependencies` | Adds an edge idempotently after graph validation. |
| `DELETE` | `/dependencies/{predecessorTaskId}/{successorTaskId}` | Removes an edge idempotently. |

Create body:

```json
{
  "predecessorTaskId": "00000000-0000-0000-0000-000000000001",
  "successorTaskId": "00000000-0000-0000-0000-000000000002"
}
```

Each list/create response includes predecessor and successor snapshots plus:

```json
{
  "isAtRisk": true,
  "risks": [
    {
      "code": "predecessor_overdue",
      "message": "The predecessor is overdue and may delay this task."
    }
  ]
}
```

Stable risk codes are `predecessor_incomplete`, `predecessor_overdue`, and
`deadline_conflict`. Warnings are read-only projections; the API never changes
a deadline automatically.

Relevant conflict codes include `project_archived`, `sprint_name_conflict`,
`sprint_has_tasks`, `task_archived`, `self_dependency`, and `dependency_cycle`.
