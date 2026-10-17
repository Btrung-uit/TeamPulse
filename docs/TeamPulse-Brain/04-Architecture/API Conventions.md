---
id: TP-ARCH-006
type: specification
status: accepted
area: architecture
tags: [teampulse/architecture, api]
created: 2026-10-14
updated: 2026-10-17
---

# API Conventions

API dùng resource-oriented REST, consistent error envelope, server-side pagination/filter/sort và optimistic concurrency token cho update. Workspace/project context phải hiện rõ trong route hoặc được xác thực từ resource ownership.

Error response dùng envelope ổn định:

```json
{
  "code": "invalid_credentials",
  "message": "Invalid email or password.",
  "errors": {
    "email": ["Enter a valid email address."]
  }
}
```

`code` dành cho client branching, `message` an toàn để hiển thị, còn `errors` là map field tùy chọn.

`TP-022` chốt pagination contract đầu tiên: list response trả `items`, `page`, `pageSize`, `totalCount`, `totalPages`; page bắt đầu từ `1`, page size trong `1..100`. Filter nằm trong query string để frontend có thể giữ state trong URL.

Mutable Task trả `rowVersion` dạng Base64. Client phải gửi lại token này khi update, đổi status hoặc archive; token cũ nhận `409 task_conflict` và phải reload trước khi retry.

Related: [[Backend Boundaries]], [[Concurrency and Archive]], [[Open Questions]].
