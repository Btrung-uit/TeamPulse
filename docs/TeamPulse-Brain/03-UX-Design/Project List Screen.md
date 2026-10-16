---
id: TP-UX-016
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, project]
created: 2026-10-14
updated: 2026-10-16
---

# Project List Screen

Project List có workspace switcher, search, active/archived groups và action `New project`. Card hiển thị name, progress, members, due/blocked signal và last updated; click card đặt active project context.

Trong `TP-013`, card chỉ hiển thị dữ liệu thật đã tồn tại: name, description, archive state và last updated. Progress, member involvement, deadline và blocked signal được hoãn tới khi task/sprint domain cung cấp query đáng tin cậy; UI không hiển thị số liệu mẫu. Leader có create/archive action và confirm modal; Member/Advisor nhận read-only list.

Project switcher ở topbar chỉ đưa active project vào danh sách chọn nhanh, luôn có lối về Project List, và điều hướng bằng URL context để refresh/deep link giữ đúng project.

Related: [[Application Shell]], [[Empty State Component]].
