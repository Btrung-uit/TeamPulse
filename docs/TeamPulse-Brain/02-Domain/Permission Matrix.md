---
id: TP-DOM-013
type: rule
status: accepted
area: domain
tags: [teampulse/domain, permissions]
created: 2026-10-14
updated: 2026-10-17
---

# Permission Matrix

| Capability | Leader | Member | Advisor |
|---|---:|---:|---:|
| View workspace and members | Yes | Yes | Yes |
| Add member/change role | Yes | No | No |
| Manage project/sprint | Yes | No | No |
| Create task | Yes | No | No |
| Change assignee/deadline | Yes | No | No |
| Update own task state | Yes | Yes | No |
| Archive task | Yes | No | No |
| Import/export Excel | Yes | No | No |
| View reports/presentation | Yes | Limited | Yes |

Phần membership/RBAC được triển khai trong `TP-012`; `TP-022` chốt quyền Task MVP. Leader quản lý toàn bộ nội dung, assignment, deadline và archive. Member chỉ đổi trạng thái task được giao, không thể đưa task sang `Done`; reviewer được chỉ định có thể chuyển `In Review → Needs Changes`. Advisor chỉ đọc. Domain lifecycle vẫn được kiểm tra sau authorization.

Related: [[Workspace and Membership]], [[Identity and Authorization]], [[Open Questions]].
