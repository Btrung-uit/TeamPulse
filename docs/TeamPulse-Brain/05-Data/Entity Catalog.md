---
id: TP-DATA-002
type: model
status: accepted
area: data
tags: [teampulse/data, entities]
created: 2026-10-14
updated: 2026-10-16
---

# Entity Catalog

Foundation entities đã triển khai trong `TP-010`:

`User`, `Workspace`, `WorkspaceMember`, `Project`, `Sprint`.

Các entity dự kiến cho các milestone tiếp theo:

`Task`, `TaskDependency`, `TaskEvidence`, `TaskComment`, `ActivityLog`, `Resource`, `ImportBatch`, `ImportRow`, `Notification`.

Phase 2 thêm `ConnectedRepository`, `GitHubDelivery`, `PullRequestSnapshot`, `CommitEvidence` và integration credentials.

Workspace là tenant boundary. Project và Sprint mang `WorkspaceId`; khóa ngoại ghép bảo đảm creator/member và project/sprint không thể tham chiếu xuyên workspace. Chi tiết cardinality và constraint nằm tại `docs/diagrams/m1-foundation-erd.md`.

Related: [[Workspace Data Ownership]], [[Open Questions]].
