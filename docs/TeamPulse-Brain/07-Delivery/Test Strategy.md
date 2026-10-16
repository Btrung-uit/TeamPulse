---
id: TP-DEL-003
type: strategy
status: accepted
area: delivery
tags: [teampulse/delivery, testing]
created: 2026-10-14
updated: 2026-10-16
---

# Test Strategy

- **Unit:** task lifecycle, dependency, permissions, contribution rules và import validation.
- **Integration:** API authorization, EF Core constraints, concurrency, workspace isolation và transactional import.
- **Frontend:** component states, form behavior và feature integration.
- **E2E smoke:** leader creates/imports work, member updates task, evidence is visible, presentation opens.

Test tập trung business risk thay vì chỉ tối đa coverage percentage.

## Milestone 1 security matrix

`TenantIdentityBoundaryTests` dùng public HTTP API để tạo hai workspace và project độc lập, sau đó kiểm tra:

- unauthenticated/invalid identity → `401`;
- authenticated nhưng không có active membership trong route workspace → `403`;
- có workspace membership nhưng dùng foreign resource ID → `404`;
- `Member`/`Advisor` read được nhưng mọi project/membership mutation → `403`;
- role hoặc active membership đổi trong database phải có hiệu lực với JWT đang tồn tại;
- request bị từ chối không thay đổi resource và không tạo project activity.

Khi thêm resource workspace-scoped mới, suite phải mở rộng cùng ma trận thay vì chỉ test happy path của endpoint mới.

Related: [[Definition of Done]], [[Task Data Integrity]], [[Excel Import Transaction]].
