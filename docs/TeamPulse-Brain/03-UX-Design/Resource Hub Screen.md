---
id: TP-UX-018
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, resources]
created: 2026-10-14
updated: 2026-10-18
---

# Resource Hub Screen

Resource Hub có search/type filter và list/table gồm name, type, linked task, owner, updated time. Selected resource mở preview metadata với actions Open external, Copy link, Edit metadata.

Mobile chuyển thành card list và chỉ giữ search/open/copy.

Implementation dùng project-scoped API và URL query cho search/type filter. Leader có Add/Edit/Remove metadata với validation và confirm modal; Member giữ read-only view. Loading dùng table skeleton, empty state phân biệt first-use/filter, API error có retry. Preview luôn nhắc rằng TeamPulse không thay thế editor của dịch vụ gốc.

Related: [[External Resource Links]], [[Table Component]].
