---
id: TP-DOM-003
type: rule
status: accepted
area: domain
tags: [teampulse/domain, task]
created: 2026-10-14
updated: 2026-10-16
---

# Task Identity

Mỗi task có database ID nội bộ và Task Code đọc được bởi con người, unique trong Project. Task Code dùng dạng `TP-123` để liên kết commit, PR, import row và evidence; GitHub issue number không phải Task Code.

`TaskCode` được trim, chuyển thành chữ hoa và phải khớp `PREFIX-NUMBER`: prefix bắt đầu bằng chữ, dài 2–10 ký tự chữ/số; phần số dài 1–6 chữ số. Ví dụ hợp lệ: `TP-001`, `TEAM2-42`. Uniqueness được cưỡng chế bởi index `(WorkspaceId, ProjectId, Code)`.

Trong C#, entity có tên `ProjectTask` để không đụng `System.Threading.Tasks.Task`; thuật ngữ sản phẩm, bảng SQL và API vẫn là **Task**.

Related: [[Task Lifecycle]], [[GitHub Collaboration Automation]], [[Task Data Integrity]].
