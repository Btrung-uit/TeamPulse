---
id: TP-DOM-007
type: concept
status: accepted
area: domain
tags: [teampulse/domain, activity]
created: 2026-10-14
updated: 2026-10-16
---

# Activity Log

Activity Log là dòng thời gian bất biến của thay đổi có ý nghĩa do user hoặc automation tạo ra. Activity khác Notification: activity là historical fact; notification là lời nhắc dành cho một người dùng cụ thể.

`TP-012` triển khai entity `ActivityLog` với workspace, actor, action, entity target, subject user, previous/new value và thời điểm xảy ra. Tạo workspace, thêm member và đổi role đều ghi activity trong cùng lần lưu mutation.

Related: [[Notification]], [[Concurrency and Archive]].
