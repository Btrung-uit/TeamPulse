---
id: TP-DATA-003
type: rule
status: accepted
area: data
tags: [teampulse/data, integrity]
created: 2026-10-14
updated: 2026-10-16
---

# Task Data Integrity

- Task Code được normalize và unique trong Project bằng index `(WorkspaceId, ProjectId, Code)`.
- Creator, assignee và reviewer phải là Workspace Member cùng tenant nhờ composite foreign key; application service của `TP-022` còn phải kiểm tra membership đang active.
- Sprint và hai đầu dependency phải cùng Workspace + Project với Task bằng composite foreign key.
- Self-dependency và dependency cycle trực tiếp/gián tiếp bị từ chối trước khi lưu.
- Status transition phải qua `ProjectTask.TransitionTo`, không update chuỗi tùy ý.
- `Status`, `Priority`, archive pair, blocked pair và estimate range có database check constraint làm lớp bảo vệ thứ hai.

Related: [[Task Identity]], [[Task Lifecycle]], [[Task Dependency]].
