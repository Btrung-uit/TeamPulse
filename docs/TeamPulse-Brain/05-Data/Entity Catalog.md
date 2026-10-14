---
id: TP-DATA-002
type: model
status: proposed
area: data
tags: [teampulse/data, entities]
created: 2026-10-14
updated: 2026-10-14
---

# Entity Catalog

Core entities dự kiến:

`User`, `Workspace`, `WorkspaceMember`, `Project`, `Sprint`, `Task`, `TaskDependency`, `TaskEvidence`, `TaskComment`, `ActivityLog`, `Resource`, `ImportBatch`, `ImportRow`, `Notification`.

Phase 2 thêm `ConnectedRepository`, `GitHubDelivery`, `PullRequestSnapshot`, `CommitEvidence` và integration credentials.

ERD, cardinality và delete behavior cần được chốt trước migration đầu tiên.

Related: [[Workspace Data Ownership]], [[Open Questions]].
