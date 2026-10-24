---
id: TP-UX-013
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, github]
created: 2026-10-14
updated: 2026-10-24
---

# PR Waiting Room Screen

Screen hiển thị filter All/Waiting/Changes/Merged. Mỗi PR card có repository/number/title, linked tasks, TeamPulse reviewer hoặc GitHub requested reviewers, age, checks, next action và `Open on GitHub`. Summary cards giữ tổng Waiting, Changes needed và Merged ngay cả khi đang lọc.

TeamPulse điều phối attention; review/merge code vẫn xảy ra trên GitHub. Screen polling mỗi 30 giây và có manual Refresh; trạng thái loading, empty và error đều có recovery rõ ràng.

Related: [[PR Waiting Room]], [[Notification Center Screen]].
