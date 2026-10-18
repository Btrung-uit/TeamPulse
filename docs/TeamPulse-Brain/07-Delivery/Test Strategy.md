---
id: TP-DEL-003
type: strategy
status: accepted
area: delivery
tags: [teampulse/delivery, testing]
created: 2026-10-14
updated: 2026-10-18
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

## Milestone 2 task board coverage

`TaskBoardFlow.test.tsx` bảo vệ các hành vi frontend có rủi ro cao:

- đọc tiếp mọi page API trước khi render board, không âm thầm bỏ task sau page đầu;
- task xuất hiện ở đúng lifecycle column và request gửi Bearer token;
- filter thay đổi query string nhưng giữ nguyên workspace/project deep link;
- Leader tạo task với payload đã normalize và query được refresh sau mutation;
- Member không thấy `New task` hoặc `Import Excel` để tránh UI hứa quyền không có.

## Milestone 3 evidence and review coverage

`TaskLifecycleTests`, `TaskEndpointsTests` và `TaskBoardFlow.test.tsx` bảo vệ:

- URL relative, `javascript:` và protocol ngoài HTTP/HTTPS bị từ chối;
- chỉ assignee hoặc Leader gắn evidence, evidence không tự hoàn thành task;
- chỉ Leader ghi quyết định review khi task đang `In Review`;
- accept/request changes lưu comment, đổi status và tạo activity trong cùng mutation;
- Task Detail hiển thị external link an toàn, review history và form theo đúng quyền.

## Milestone 3 dashboard coverage

`TaskEndpointsTests` và `DashboardFlow.test.tsx` bảo vệ:

- KPI, workload, priority tasks và contribution chỉ tổng hợp trong project hiện tại;
- foreign workspace trả `403`, project ID ngụy trang qua workspace hợp lệ trả `404`;
- accepted task/evidence/review là các cột độc lập, response không có commit count hoặc quality score;
- KPI dẫn tới Task Board với filter open, review hoặc attention tương ứng;
- dữ liệu thật, first-use state và các nhánh loading/error được render ổn định.

## Milestone 3 Resource Hub coverage

`ResourceEndpointsTests` và `ResourceHubFlow.test.tsx` bảo vệ:

- URL chỉ nhận HTTP/HTTPS; owner là active workspace member và linked task thuộc cùng project;
- mọi member đọc được nhưng Resource metadata mutation chỉ dành cho Leader;
- search không phân biệt hoa thường, type filter và project/tenant boundary không làm rò dữ liệu;
- liên kết Resource với task không tự tạo Task Evidence;
- desktop có metadata preview/manage actions, mobile giữ search/open/copy và Member dùng read-only mode.

## Milestone 3 Git Command Generator coverage

`gitCommandModel.test.ts` và `GitCommandGenerator.test.tsx` bảo vệ:

- branch và commit luôn chứa Task Code, đồng thời branch type và commit type có thể đổi;
- tiêu đề tiếng Việt được chuyển thành branch slug ổn định và commit summary được chuẩn hóa;
- generator không bao giờ tạo `git add .` hoặc push trực tiếp vào `main`;
- chỉ explicit relative paths hợp lệ được stage; absolute path, traversal, wildcard và shell metacharacter bị từ chối;
- thiếu path thì không sinh stage command và `Copy all` bị khóa;
- copy từng command tạo success toast, lỗi clipboard tạo error toast;
- entry point chỉ xuất hiện trong Task Detail management mode trên desktop và được ẩn ở mobile.

Related: [[Definition of Done]], [[Task Data Integrity]], [[Excel Import Transaction]].
