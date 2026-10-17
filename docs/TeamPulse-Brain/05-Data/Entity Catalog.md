---
id: TP-DATA-002
type: model
status: accepted
area: data
tags: [teampulse/data, entities]
created: 2026-10-14
updated: 2026-10-17
---

# Entity Catalog

Các entity đã triển khai qua `TP-010` đến `TP-020`:

`User`, `Workspace`, `WorkspaceMember`, `Project`, `Sprint`, `ActivityLog`, `ProjectTask` (Task trong domain sản phẩm), `TaskDependency`, `TaskEvidence`, `TaskComment`.

Các entity dự kiến cho các milestone tiếp theo:

`Resource`, `ImportBatch`, `ImportRow`, `Notification`.

Phase 2 thêm `ConnectedRepository`, `GitHubDelivery`, `PullRequestSnapshot`, `CommitEvidence` và integration credentials.

Workspace là tenant boundary. Project, Sprint, Task, TaskDependency, TaskEvidence và TaskComment mang `WorkspaceId`; khóa ngoại ghép bảo đảm creator/member, project/sprint, evidence, review và dependency không thể tham chiếu xuyên workspace hoặc project. Chi tiết nền M1 nằm tại `docs/diagrams/m1-foundation-erd.md`; task aggregate nằm tại `docs/diagrams/m2-task-domain-erd.md`.

Related: [[Workspace Data Ownership]], [[Open Questions]].
