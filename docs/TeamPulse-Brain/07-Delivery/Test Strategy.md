---
id: TP-DEL-003
type: strategy
status: accepted
area: delivery
tags: [teampulse/delivery, testing]
created: 2026-10-14
updated: 2026-10-17
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

## Milestone 2 planning coverage

`TaskPlanningDomainTests` và `PlanningEndpointsTests` bảo vệ:

- khoảng ngày Sprint và việc không update dở dang khi input sai;
- CRUD Sprint, task count, gán/bỏ sprint và không xóa Sprint còn task;
- duplicate dependency idempotent, self-reference/cross-project/cycle bị từ chối;
- risk codes phản ánh state/deadline hiện tại nhưng không tự thay deadline;
- `Leader` mutate, `Member`/`Advisor` read-only, foreign workspace `403` và disguised foreign project `404`;
- planning mutation tạo activity log.

## Milestone 2 task API coverage

`TaskEndpointsTests` chạy qua public HTTP API và kiểm tra:

- create/detail/list, server pagination và kết hợp task filters;
- code trùng bị từ chối và code đã tạo là immutable;
- full update chỉ dành cho Leader, quan hệ assignee/reviewer/sprint đúng tenant;
- assignee, reviewer, Leader và Advisor có đúng quyền status đã chốt;
- invalid transition và stale `RowVersion` trả conflict ổn định, không đổi task;
- archive idempotent, bị ẩn mặc định nhưng giữ detail và activity;
- foreign workspace `403`, disguised foreign project/task `404`;
- activity create/assign/deadline/sprint/update/status/archive được tạo đúng, không nhân đôi.

Related: [[Definition of Done]], [[Task Data Integrity]], [[Excel Import Transaction]].
