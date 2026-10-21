---
id: TP-UX-019
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, notifications]
created: 2026-10-14
updated: 2026-10-21
---

# Notification Center Screen

Desktop notification là panel khoảng `400px`; mobile là full-screen sheet. Tabs gồm All, Unread và Reviews; item nhóm theo thời gian, có unread marker, concise context và deep-link action.

Không tạo notification cho mọi activity nhỏ; hỗ trợ Mark all as read.

MVP nối chuông trên topbar và route Inbox mobile vào feed canonical của workspace. Mỗi item mở đúng project/task, click item lưu read receipt; tabs All, Unread và Reviews cùng unread badge dùng dữ liệu backend thay vì số hard-code. Realtime delivery chưa thuộc MVP nên client reload feed khi đổi workspace hoặc mở panel.

Related: [[Notification]], [[Realtime Updates]], [[Mobile Navigation]].
