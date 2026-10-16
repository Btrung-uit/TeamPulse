---
id: TP-ARCH-006
type: specification
status: accepted
area: architecture
tags: [teampulse/architecture, api]
created: 2026-10-14
updated: 2026-10-16
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

`code` dành cho client branching, `message` an toàn để hiển thị, còn `errors` là map field tùy chọn. Pagination contract sẽ được bổ sung khi triển khai list endpoint đầu tiên.

Related: [[Backend Boundaries]], [[Concurrency and Archive]], [[Open Questions]].
