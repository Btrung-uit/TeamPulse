---
id: TP-UX-020
type: specification
status: accepted
area: ux
tags: [teampulse/ux, mobile]
created: 2026-10-14
updated: 2026-10-17
---

# Mobile Navigation

Bottom navigation có `Home`, `Tasks`, `Inbox`, `More`. `More` chứa PRs, Resources, Presentation, Profile và Theme; project switcher nằm ở mobile header.

Navigation ưu tiên deep links từ Inbox tới đúng task/PR. Không đưa admin/import/configuration vào mobile menu.

`TP-025` nối `Tasks` tới [[My Tasks Screen]], giữ `Inbox` ở placeholder cho milestone notification và triển khai `More` với PR status, Resources, Presentation, profile, theme và sign out. `More` không chứa Excel import, workspace/member administration hoặc integration setup.

Related: [[Desktop First and Mobile Monitoring]], [[Notification Center Screen]].
