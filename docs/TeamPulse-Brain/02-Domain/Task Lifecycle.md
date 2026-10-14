---
id: TP-DOM-004
type: rule
status: accepted
area: domain
tags: [teampulse/domain, workflow]
created: 2026-10-14
updated: 2026-10-14
---

# Task Lifecycle

```text
Todo → In Progress → Evidence Linked → In Review → Done
```

`Blocked` và `Needs Changes` là trạng thái ngoại lệ có đường quay lại luồng làm việc. Commit chỉ tạo evidence; PR ready đưa task vào review; changes requested đưa task sang Needs Changes; PR merged hoặc leader xác nhận mới tạo Done.

Archived loại task khỏi active planning nhưng giữ toàn bộ lịch sử.

Related: [[Evidence and Review]], [[Activity Log]], [[GitHub Collaboration Automation]].
