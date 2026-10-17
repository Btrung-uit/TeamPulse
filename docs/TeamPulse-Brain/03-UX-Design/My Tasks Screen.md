---
id: TP-UX-029
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, tasks, mobile]
created: 2026-10-17
updated: 2026-10-17
---

# My Tasks Screen

`My Tasks` là personal execution queue, chỉ tải task có `assigneeUserId` bằng user đang đăng nhập. Mặc định ẩn `Done`; search, status và deadline bucket được giữ trong query string để refresh/deep link không mất context.

Thứ tự chú ý là `Blocked → Overdue → Needs Changes → Due in 7 days → priority → deadline`. Summary cards hiển thị Active, Blocked, Overdue và Due in 7 days. Desktop dùng grid tối đa ba cột; mobile dùng card list một cột, không Kanban hoặc drag-and-drop.

Task click mở [[Task Detail Screen]] ở monitoring mode: đọc đầy đủ và cho status transition theo quan hệ assignee/reviewer, nhưng không edit planning/content, dependency hoặc archive. Full management chỉ nằm ở Task Board desktop.

Related: [[Task Board Screen]], [[Desktop First and Mobile Monitoring]], [[Mobile Navigation]].
