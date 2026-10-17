---
id: TP-UX-012
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, tasks]
created: 2026-10-14
updated: 2026-10-17
---

# Task Detail Screen

Desktop mở task trong right side panel để giữ context của board; mobile dùng full page/bottom sheet. Nội dung: code/title, status/priority, assignee/reviewer, deadline/estimate/dependency, description, evidence, activity và comments.

Permission quyết định field được edit. Task có evidence/history chỉ được archive, không hard-delete.

`TP-024` triển khai card mở panel qua query param `task`, giữ nguyên search/filter/sort của board. Desktop dùng right side panel; dưới `768px` panel chiếm toàn viewport. Leader được sửa nội dung, planning, dependency và archive; Member chỉ có các transition mà quan hệ assignee/reviewer cho phép; Advisor đọc-only. Task archive luôn cần confirm modal.

Mọi mutation gửi `RowVersion`. Khi API trả `task_conflict`, draft của người dùng không bị xóa và UI giải thích task đã đổi ở nơi khác. `Review latest` tải lại dữ liệu server; `Retry my action` là lựa chọn explicit dùng token mới nhất, không tự động ghi đè ngầm. Detail trả thêm `statusBeforeBlocked` để client chỉ hiển thị đúng transition phục hồi từ `Blocked`.

Related: [[Task Board Screen]], [[Evidence and Review]], [[Permission Matrix]].
