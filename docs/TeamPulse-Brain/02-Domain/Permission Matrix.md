---
id: TP-DOM-013
type: rule
status: accepted
area: domain
tags: [teampulse/domain, permissions]
created: 2026-10-14
updated: 2026-10-16
---

# Permission Matrix

| Capability | Leader | Member | Advisor |
|---|---:|---:|---:|
| View workspace and members | Yes | Yes | Yes |
| Add member/change role | Yes | No | No |
| Manage project/sprint | Yes | No | No |
| Create task | Yes | Configurable | No |
| Change assignee/deadline | Yes | No | No |
| Update own task state | Yes | Yes | No |
| Archive task | Yes | No | No |
| Import/export Excel | Yes | No | No |
| View reports/presentation | Yes | Limited | Yes |

Phần membership/RBAC đã được triển khai trong `TP-012`. Quyền tạo task của Member vẫn là quyết định riêng cần user validation trước `TP-020`.

Related: [[Workspace and Membership]], [[Identity and Authorization]], [[Open Questions]].
