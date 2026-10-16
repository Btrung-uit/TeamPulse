---
id: TP-DOM-005
type: rule
status: accepted
area: domain
tags: [teampulse/domain, dependency]
created: 2026-10-14
updated: 2026-10-16
---

# Task Dependency

Dependency biểu diễn task bị ảnh hưởng bởi tiến độ task khác. Khi deadline của predecessor thay đổi, TeamPulse cảnh báo các successor có rủi ro; MVP không tự động dời deadline vì lịch làm việc và loại dependency chưa đủ thông tin để quyết định thay leader.

`TaskDependency` dùng khóa `(WorkspaceId, ProjectId, PredecessorTaskId, SuccessorTaskId)`. Hai khóa ngoại ghép cùng trỏ tới `Task`, vì vậy database từ chối dependency chéo workspace/project và self-reference bị chặn ở cả domain lẫn check constraint. Phát hiện dependency cycle và cảnh báo deadline thuộc `TP-021`, không nằm trong entity nền của `TP-020`.

Related: [[Task Lifecycle]], [[Task Data Integrity]].
