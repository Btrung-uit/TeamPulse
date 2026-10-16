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

`TaskDependency` dùng khóa `(WorkspaceId, ProjectId, PredecessorTaskId, SuccessorTaskId)`. Hai khóa ngoại ghép cùng trỏ tới `Task`, vì vậy database từ chối dependency chéo workspace/project và self-reference bị chặn ở cả domain lẫn check constraint.

Trước khi thêm cạnh `predecessor → successor`, `TaskDependencyGraph` tìm đường từ successor quay về predecessor. Nếu có đường, cạnh mới tạo cycle trực tiếp hoặc gián tiếp và bị trả `dependency_cycle`. Thêm lại đúng một cạnh là idempotent.

API tính lại cảnh báo từ dữ liệu hiện tại mỗi lần đọc:

- `predecessor_incomplete`: predecessor chưa `Done`;
- `predecessor_overdue`: predecessor chưa `Done` và đã quá hạn;
- `deadline_conflict`: deadline predecessor nằm sau deadline successor.

Cảnh báo chỉ cung cấp thông tin cho Leader. Nó không đổi status, sprint hoặc deadline của bất kỳ task nào; quyết định điều chỉnh vẫn thuộc về con người.

Related: [[Task Lifecycle]], [[Task Data Integrity]].
