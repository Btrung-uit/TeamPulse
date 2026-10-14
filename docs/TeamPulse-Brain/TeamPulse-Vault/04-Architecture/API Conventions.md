---
id: TP-ARCH-006
type: specification
status: proposed
area: architecture
tags: [teampulse/architecture, api]
created: 2026-10-14
updated: 2026-10-14
---

# API Conventions

API dùng resource-oriented REST, consistent error envelope, server-side pagination/filter/sort và optimistic concurrency token cho update. Workspace/project context phải hiện rõ trong route hoặc được xác thực từ resource ownership.

Chi tiết status code, error schema và pagination contract cần được chốt trước khi implement controller đầu tiên.

Related: [[Backend Boundaries]], [[Concurrency and Archive]], [[Open Questions]].
