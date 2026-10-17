---
id: TP-DOM-004
type: rule
status: accepted
area: domain
tags: [teampulse/domain, workflow]
created: 2026-10-14
updated: 2026-10-17
---

# Task Lifecycle

```text
Todo → In Progress → Evidence Linked → In Review → Done
```

Các transition được triển khai trong `ProjectTask`, không cho application layer ghi status tùy ý:

- `Todo → In Progress`.
- `In Progress → Todo | Evidence Linked`.
- `Evidence Linked → In Progress | In Review`.
- `In Review → In Progress | Needs Changes | Done`.
- `Needs Changes → In Progress | In Review`.
- Mọi trạng thái active trừ `Done` có thể vào `Blocked`; task chỉ được mở block về đúng trạng thái trước đó.
- `Done` là trạng thái kết thúc và không có transition đi ra.

Commit/evidence chỉ có thể đưa task tới `Evidence Linked`; do đó không có đường `Evidence Linked → Done`. PR ready hoặc review thủ công đưa task vào `In Review`; changes requested đưa task sang `Needs Changes`; PR merged hoặc leader xác nhận review mới đưa task từ `In Review` sang `Done`.

`Archived` không phải workflow status. `IsArchived` + `ArchivedAtUtc` loại task khỏi active planning, giữ nguyên status và lịch sử; task archive không thể chỉnh sửa.

Task API thực thi thêm ranh giới vai trò: assignee Member đi theo workflow của task mình trừ `Done`, reviewer được chỉ định mới có quyền yêu cầu sửa, và Leader là người duy nhất xác nhận `Done`. Mỗi transition thành công tạo một activity với trạng thái trước/sau; transition lặp lại cùng trạng thái không tạo log nhiễu.

Related: [[Evidence and Review]], [[Activity Log]], [[GitHub Collaboration Automation]].
