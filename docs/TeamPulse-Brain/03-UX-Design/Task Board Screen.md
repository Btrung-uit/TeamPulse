---
id: TP-UX-011
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen, tasks]
created: 2026-10-14
updated: 2026-10-17
---

# Task Board Screen

Board gồm toolbar search/filter/view và các cột status chứa Task Card. Drag-and-drop chỉ có trên desktop và phải tôn trọng [[Task Lifecycle]]; mobile chuyển thành filtered card list, không kéo thả.

Task click mở [[Task Detail Screen]]. Empty board ưu tiên `New task`, action phụ là `Import Excel`.

`TP-023` triển khai desktop board với đủ bảy cột lifecycle, horizontal scroll để giữ card đủ rộng, skeleton/error/empty/filtered-empty state và task creation modal cho Leader. Search, assignee, sprint, priority và sort được giữ trong query string của deep link project; query client đọc toàn bộ các trang API trước khi sort và chia cột.

Drag-and-drop chưa được mở ở `TP-023`. Dù domain đã chặn transition sai, UX cho mutation còn cần permission và `RowVersion` conflict handling của `TP-024`; vì vậy card hiện chỉ đọc để tránh tạo một đường cập nhật trạng thái không an toàn. Mobile trong slice này giữ board cuộn ngang; filtered card list chuyên biệt thuộc `TP-025`.

Related: [[Task Card Component]], [[Excel Import Wizard Screen]].
