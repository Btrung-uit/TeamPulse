# TeamPulse — MVP Delivery Backlog

> **Mục tiêu:** trong 10 tuần, xây một TeamPulse có thể demo trọn luồng: leader tạo workspace/project hoặc import Excel → giao task → member cập nhật/gắn evidence → leader review → dashboard, resource hub và presentation phản ánh dữ liệu thật.

## Cách dùng backlog

- Mỗi mã `TP-xxx` là một task có thể tạo thành GitHub Issue và một nhánh `feature/TP-xxx-ten-ngan`.
- Làm theo thứ tự phase. Chỉ kéo task kế tiếp khi task phụ thuộc đã đạt acceptance criteria.
- Ưu tiên `P0` trước. `P1` chỉ làm khi toàn bộ P0 trong cùng milestone ổn định.
- Quy ước commit: Conventional Commits và ghi Task Code khi liên quan, ví dụ `feat(tasks): add task board (TP-022)`.
- `main` phải luôn chạy được; phát triển trên feature branch → Pull Request → `develop` (hoặc `main` khi làm một mình và chưa cần nhánh tích hợp).

## Definition of Done chung

Một task chỉ được đánh dấu Done khi:

- [ ] Acceptance criteria bên dưới đã đúng với role phù hợp.
- [ ] Có xử lý loading, empty, validation/error và no-permission nếu task có UI/API.
- [ ] Có test phù hợp hoặc lý do rõ ràng vì sao chưa tự động hóa được.
- [ ] Không lộ secret; không có lỗi console hoặc lỗi API nghiêm trọng ở happy path.
- [ ] Responsive/keyboard được kiểm tra nếu có giao diện.
- [ ] Cập nhật README, ADR hoặc atomic note khi thay đổi domain/decision.

## Phạm vi khóa cho MVP

| Làm trong MVP | Để Phase 2 hoặc sau đó |
| --- | --- |
| Auth, workspace, project, role, task/sprint, dependency warning | GitHub App, webhook, tự động đổi status theo PR |
| Activity log, evidence link thủ công, contribution summary | PR Waiting Room đồng bộ GitHub |
| Dashboard, Excel import/export, Resource Hub, Presentation Mode | SignalR và notification nâng cao |
| Git Command Generator chỉ tạo/copy hướng dẫn | Drive API, đồng bộ Docs/Canva/Drive hai chiều |

---

# Milestone 0 — Chuẩn bị dự án (Tuần 1)

**Mục tiêu kiểm chứng:** clone repository xong, một người khác có thể chạy frontend, API và SQL Server local theo README.

## TP-001 — Khóa phạm vi và tiêu chí demo

- **Ưu tiên:** P0 · **Ước lượng:** 0.5 ngày · **Phụ thuộc:** không có
- **Thực hiện:** Chuyển nội dung của `TASKS.md` thành GitHub Issues/milestones; chọn một demo project giả lập 4 thành viên, 15–25 task và một file Excel mẫu.
- **Acceptance criteria:** Có demo story 5 phút; MVP/Phase 2 được phân biệt rõ; không có feature “clone Google Docs/Drive/Canva”.
- **Đầu ra:** GitHub Project/Issues, dữ liệu demo và tiêu chí “MVP đủ để deploy”.

## TP-002 — Khởi tạo solution và frontend

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-001
- **Thực hiện:** Tạo ASP.NET Core solution gồm `Api`, `Application`, `Domain`, `Infrastructure`, unit/integration tests; tạo React + TypeScript app; thiết lập lint, format và aliases tối thiểu.
- **Acceptance criteria:** `dotnet build` và `npm run build` chạy thành công từ checkout mới; API trả `/health`; frontend hiển thị trang placeholder.
- **Đầu ra:** Source code chạy được thay cho các `.gitkeep`.

## TP-003 — Local infrastructure và cấu hình an toàn

- **Ưu tiên:** P0 · **Ước lượng:** 0.75 ngày · **Phụ thuộc:** TP-002
- **Thực hiện:** Thêm Docker Compose cho SQL Server và API/frontend khi phù hợp; tạo `.env.example`, cấu hình development và secret management local; bổ sung health check.
- **Acceptance criteria:** Không có secret thật trong Git; một lệnh được ghi trong README khởi động được database local; API kết nối database thành công.
- **Đầu ra:** `docker-compose.yml` (hoặc tương đương), env example, hướng dẫn run local.

## TP-004 — Thiết lập CI chất lượng tối thiểu

- **Ưu tiên:** P0 · **Ước lượng:** 0.5 ngày · **Phụ thuộc:** TP-002
- **Thực hiện:** GitHub Actions chạy build backend, build frontend và test khi có test; bật branch protection khi có collaborator.
- **Acceptance criteria:** PR có check tự động; check thất bại khi build/test thất bại; workflow không dùng secret trong log.
- **Đầu ra:** Workflow CI và badge/trạng thái cập nhật trong README.

## TP-005 — Dựng design system và application shell

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-002
- **Thực hiện:** Cài routing, query client, global error boundary; triển khai tokens dark/light, typography, spacing, radius và shell desktop-first (sidebar/topbar/content).
- **Acceptance criteria:** Theme sáng/tối đúng token; route có loading/error fallback; desktop shell hoạt động ở màn hình phổ biến.
- **Đầu ra:** `app/`, `styles/`, layout shell và token CSS/TypeScript.

## TP-006 — Component primitives

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-005
- **Thực hiện:** Xây `Button`, `Input`, `StatusBadge`, `Modal`, `Toast`, `EmptyState`; sau đó là `TaskCard` và `Table` theo component specification đã chốt.
- **Acceptance criteria:** Mỗi component có default, hover, focus-visible, disabled/loading và accessibility label cần thiết; không dùng màu đơn thuần để truyền trạng thái.
- **Đầu ra:** UI primitives tái sử dụng và trang/dev route trình diễn states.

**Gate M0:** `TP-002` đến `TP-006` chạy được trên máy khác. Không bắt đầu domain feature khi local run chưa ổn định.

---

# Milestone 1 — Identity, Workspace và Project (Tuần 2)

**Mục tiêu kiểm chứng:** leader tạo workspace/project, mời member và mỗi người chỉ xem được dữ liệu thuộc workspace của mình.

## TP-010 — Thiết kế schema nền và EF Core migrations

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-003
- **Thực hiện:** Model `User`, `Workspace`, `WorkspaceMember`, `Project`, `Sprint`; cấu hình khóa, index, audit timestamps và quan hệ ownership theo `CONTEXT.md`.
- **Acceptance criteria:** Migration chạy trên database trống; không thể tạo project thiếu workspace; query luôn có workspace scope.
- **Đầu ra:** Entity configurations, migration đầu tiên, ERD trong `docs/diagrams`.

## TP-011 — Authentication và session

- **Ưu tiên:** P0 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-010
- **Thực hiện:** ASP.NET Core Identity + JWT/refresh strategy phù hợp; register, login, logout, get-current-user; form frontend và protected routes.
- **Acceptance criteria:** Người chưa đăng nhập bị chuyển về login; token không nằm trong URL; lỗi email/password hiển thị an toàn; endpoint protected từ chối token không hợp lệ.
- **Đầu ra:** Auth endpoints, auth state client và Login/Register screen.

## TP-012 — Workspace membership và RBAC

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-010, TP-011
- **Thực hiện:** Role `Leader`, `Member`, `Advisor`; API tạo workspace, mời/thêm thành viên (có thể dùng email đã tồn tại), thay role và kiểm tra membership policy.
- **Acceptance criteria:** Leader không truy cập được workspace khác; Advisor không có mutation; Member không tự tăng quyền; mọi membership mutation có activity entry.
- **Đầu ra:** Authorization policies, membership endpoints và màn hình workspace onboarding.

## TP-013 — Project list, create và project switcher

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-012
- **Thực hiện:** CRUD project trong workspace, project list/empty state, project switcher và context project trong URL.
- **Acceptance criteria:** Project thuộc đúng workspace; leader tạo/archive project; member chỉ thấy project được phép; deep link project hoạt động sau refresh.
- **Đầu ra:** Project List screen và project context provider.

## TP-014 — Tests ranh giới tenant và identity

- **Ưu tiên:** P0 · **Ước lượng:** 0.75 ngày · **Phụ thuộc:** TP-011, TP-012, TP-013
- **Thực hiện:** Viết integration tests cho auth, role và cross-workspace access; seed hai workspace để chứng minh cách ly dữ liệu.
- **Acceptance criteria:** Có test thất bại nếu API trả dữ liệu workspace khác; các role cấm mutation bị trả `403`.
- **Đầu ra:** Identity/workspace integration test suite.

**Gate M1:** Leader A không thể đọc hoặc sửa bất kỳ project nào của Workspace B.

---

# Milestone 2 — Core work management (Tuần 3–4)

**Mục tiêu kiểm chứng:** Nhóm có thể lập kế hoạch, giao việc, theo dõi trạng thái và lưu lịch sử thay đổi mà không cần Excel sau khi khởi tạo.

## TP-020 — Task domain và lifecycle rules

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-010, TP-012
- **Thực hiện:** Model `Task`, `TaskDependency`, fields: Task Code, title, description, assignee, reviewer, status, priority, due date, estimate, `RowVersion`; implement state machine.
- **Acceptance criteria:** Task Code unique trong project; chỉ transition hợp lệ; `Done` không được tạo chỉ vì có commit/evidence; task đã có lịch sử không hard-delete.
- **Đầu ra:** Domain entities, value types/enums, unit tests lifecycle.

## TP-021 — Sprint, dependency và cảnh báo rủi ro

- **Ưu tiên:** P1 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-020
- **Thực hiện:** CRUD sprint; gán task vào sprint; tạo dependency cùng project; phát hiện cycle; cảnh báo successor bị ảnh hưởng khi predecessor/deadline đổi.
- **Acceptance criteria:** Không tạo dependency chéo project hoặc vòng lặp; hệ thống cảnh báo nhưng không tự dời deadline; UI nêu rõ task bị phụ thuộc.
- **Đầu ra:** Sprint/dependency APIs và UI properties.

## TP-022 — Task API và activity log

- **Ưu tiên:** P0 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-020
- **Thực hiện:** APIs list/filter/create/detail/update/status/archive; pagination; immutable `ActivityLog` cho create, assign, status, deadline, archive.
- **Acceptance criteria:** Mọi mutation qua domain rule và workspace authorization; `RowVersion` trả conflict rõ ràng; archive giữ được history/evidence.
- **Đầu ra:** Task module backend, API contract và integration tests.

## TP-023 — Task Board và task creation

- **Ưu tiên:** P0 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-006, TP-022
- **Thực hiện:** Kanban desktop theo status, search/filter/sort, create task modal, empty state; drag-and-drop chỉ thêm khi lifecycle enforcement đã chắc.
- **Acceptance criteria:** Board chỉ hiển thị task của project hiện tại; filters không làm mất URL/context; empty state dẫn tới `New task` hoặc `Import Excel`.
- **Đầu ra:** Task Board screen và feature query/mutation hooks.

## TP-024 — Task Detail và inline management

- **Ưu tiên:** P0 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-022, TP-023
- **Thực hiện:** Side panel desktop/full page mobile; edit title, description, assignment, priority, deadline, estimate, dependency và status theo permission.
- **Acceptance criteria:** Member chỉ thay đổi nội dung/trạng thái được cấp quyền; conflict khi hai người lưu cùng task được giải thích và có refresh/retry; archive có confirm modal.
- **Đầu ra:** Task Detail screen, confirm flows và conflict UX.

## TP-025 — My Tasks và mobile monitoring slice

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-023, TP-024
- **Thực hiện:** Trang My Tasks, lọc theo assignee/due/status; mobile bottom navigation tối giản với Home, Tasks, Inbox placeholder và More; không đưa import/admin vào mobile.
- **Acceptance criteria:** Member mở điện thoại thấy task ưu tiên, overdue/blocked và cập nhật personal status an toàn; task detail đọc được trên màn hình hẹp.
- **Đầu ra:** My Tasks view và mobile navigation responsive.

## TP-026 — Regression tests cho work management

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-020 đến TP-025
- **Thực hiện:** Unit tests lifecycle/dependency; integration tests permissions/archive/concurrency; frontend tests cho form validation và status badge/card.
- **Acceptance criteria:** Test bao phủ transition sai, cycle dependency, cross-workspace access và `RowVersion` conflict; CI chạy toàn bộ tests.
- **Đầu ra:** Core workflow test baseline.

**Gate M2:** Leader tạo task, member thực hiện, leader archive task có history; toàn bộ activity nhìn thấy theo đúng quyền.

---

# Milestone 3 — Evidence, Dashboard và Resources (Tuần 5)

**Mục tiêu kiểm chứng:** Leader có thể biết nhóm đang làm gì và chứng minh đóng góp bằng dữ liệu thật, không phải đếm commit thô.

## TP-030 — Evidence links và review thủ công

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-022, TP-024
- **Thực hiện:** Model `TaskEvidence` và `TaskComment`; gắn URL/loại evidence: commit, PR, issue, document, design, demo; leader review/accept hoặc request changes.
- **Acceptance criteria:** URL được validate; evidence không tự hoàn thành task; review action ghi activity; task đi được `Evidence Linked → In Review → Done` hoặc `Needs Changes`.
- **Đầu ra:** Evidence/review API và section trong Task Detail.

## TP-031 — Dashboard và contribution summary

- **Ưu tiên:** P0 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-022, TP-030
- **Thực hiện:** KPI progress/open/review/blocked/due soon, workload, My Priority Tasks, recent activity, contribution view dựa trên accepted task/evidence/review.
- **Acceptance criteria:** Số liệu lọc đúng theo project và không dùng commit count làm quality score; KPI click được tới danh sách task tương ứng; dashboard có empty/loading/error states.
- **Đầu ra:** Dashboard screen và reporting queries.

## TP-032 — Resource Hub

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-013, TP-024
- **Thực hiện:** Lưu title, type, URL/external ID, owner, linked task và timestamps cho Docs/Drive/Canva/GitHub/demo; search/filter và open external link.
- **Acceptance criteria:** TeamPulse chỉ quản lý metadata/link, không giả lập editor; resource workspace-scoped; có thể liên kết resource với task mà không tự biến thành evidence.
- **Đầu ra:** Resource API và Resource Hub screen.

## TP-033 — Git Command Generator

- **Ưu tiên:** P1 · **Ước lượng:** 0.75 ngày · **Phụ thuộc:** TP-020, TP-024
- **Thực hiện:** Tạo branch name và commit message có Task Code; cho copy từng lệnh và ghi hướng dẫn stage explicit paths.
- **Acceptance criteria:** Không sinh lệnh push trực tiếp vào `main`; không mặc định `git add .`; người dùng chỉnh được branch type và commit type.
- **Đầu ra:** Generator trong Task Detail và copy feedback toast.

**Gate M3:** Dashboard phản ánh task thật; mỗi task Done có thể mở được evidence hoặc manual leader confirmation trong activity.

---

# Milestone 4 — Excel onboarding và Presentation (Tuần 6)

**Mục tiêu kiểm chứng:** Leader chuyển từ spreadsheet sang TeamPulse đúng một lần, sau đó quản trị trên web và trình bày tiến độ từ dữ liệu hệ thống.

## TP-040 — Excel template và export snapshot

- **Ưu tiên:** P0 · **Ước lượng:** 0.75 ngày · **Phụ thuộc:** TP-022
- **Thực hiện:** Dùng ClosedXML tạo template/export với `TaskCode`, `Title`, `AssigneeEmail`, `Status`, `Priority`, `DueDate`, `EstimateHours`, `DependsOn`; kèm sample file.
- **Acceptance criteria:** Export khớp filter/project hiện tại; ngày/enum có format rõ; file mở được trong Excel/Google Sheets.
- **Đầu ra:** Download template và export endpoint.

## TP-041 — Excel upload, mapping và validation preview

- **Ưu tiên:** P0 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-040, TP-012
- **Thực hiện:** Upload `.xlsx`, giới hạn kích thước, đọc workbook, chọn sheet, map cột, validate required fields/member/status/date/unique Task Code/dependency; lưu `ImportBatch` và error rows.
- **Acceptance criteria:** Client accept filter không phải lớp bảo vệ duy nhất; file lỗi không tạo task; preview chỉ rõ row/column/reason; non-leader bị từ chối.
- **Đầu ra:** Import APIs và wizard bước Upload → Map → Preview lỗi.

## TP-042 — Confirm import theo transaction

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-041, TP-020
- **Thực hiện:** Confirm batch bằng database transaction; tạo toàn bộ tasks/activity hoặc không tạo gì; ngăn double-confirm; giữ audit metadata.
- **Acceptance criteria:** Một row invalid làm cả batch không commit; retry/refresh không nhân đôi task; sau confirm, web database là source of truth, re-upload không tự overwrite.
- **Đầu ra:** Confirm endpoint, modal xác nhận và import history tối thiểu.

## TP-043 — Presentation Mode

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-031, TP-032
- **Thực hiện:** Full-screen, read-only, không sidebar; project outcome, KPI/current phase, members/contribution evidence, key resources/repository/demo/slides.
- **Acceptance criteria:** Mở được từ project; Advisor chỉ đọc; giao diện đủ rõ để trình bày trong 3–5 phút; không hiển thị dữ liệu workspace khác.
- **Đầu ra:** Presentation screen và public/read-only policy phù hợp.

## TP-044 — E2E core demo flow

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-030 đến TP-043
- **Thực hiện:** E2E smoke: leader login → create/import → member cập nhật → attach evidence → leader review → dashboard/presentation.
- **Acceptance criteria:** Flow chạy trên environment sạch với seed data; lỗi ở bước nào có screenshot/log; CI có thể chạy smoke test.
- **Đầu ra:** Playwright/Cypress suite hoặc kịch bản test có thể tái chạy.

**Gate M4 — MVP Feature Complete:** Luồng demo end-to-end chạy với dữ liệu thật. Không bắt đầu GitHub webhook nếu gate này chưa đạt.

---

# Milestone 5 — Hardening, deploy và portfolio proof (Tuần 7, 9–10)

**Mục tiêu kiểm chứng:** MVP được deploy, có dữ liệu demo, có test và có câu chuyện portfolio đáng tin cậy.

## TP-050 — Hoàn thiện system states và accessibility

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** M4
- **Thực hiện:** Audit loading, empty, API error, no-permission, archive/import/deadline confirm; focus trap modal, keyboard navigation, contrast, reduced motion.
- **Acceptance criteria:** Không có màn hình “trắng” khi API chậm/lỗi; action phá hủy cần confirm; keyboard hoàn thành được workflow chính.
- **Đầu ra:** Error/state components hoàn chỉnh và accessibility checklist.

## TP-051 — Security và reliability pass

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** M4
- **Thực hiện:** Rà authz từng endpoint, CORS, rate limit tối thiểu cho auth/import, upload validation, structured logging và global exception handler.
- **Acceptance criteria:** Không lộ stack trace/secret; endpoint bị cấm trả đúng `401/403`; import sai format/size bị chặn server-side; log có correlation/request information phù hợp.
- **Đầu ra:** Security checklist và regression tests quan trọng.

## TP-052 — Docker production-like và deployment

- **Ưu tiên:** P0 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-003, TP-050, TP-051
- **Thực hiện:** Containerize frontend/API, externalize configuration, chạy migration an toàn, deploy một environment demo; thêm health endpoint và hướng dẫn rollback cơ bản.
- **Acceptance criteria:** Deploy mới từ main thành công; environment variables không được commit; demo URL chạy trên desktop/mobile; health check quan sát được.
- **Đầu ra:** Docker config, deployment guide và demo URL.

## TP-053 — Seed scenario và portfolio documentation

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-052
- **Thực hiện:** Seed “UIT team 4 người” với 15–25 task, deadlines, evidence, resource links; cập nhật README run instructions, ERD, architecture diagram, API overview và screenshots thật.
- **Acceptance criteria:** Reviewer clone repo hiểu cách chạy trong 10 phút; demo không dựa vào data thủ công khó tái tạo; README không claim feature chưa tồn tại.
- **Đầu ra:** Seed script, ảnh/GIF, README hoàn chỉnh.

## TP-054 — Demo video, retrospective và validation người dùng

- **Ưu tiên:** P0 · **Ước lượng:** 1 ngày · **Phụ thuộc:** TP-053
- **Thực hiện:** Quay video 3–5 phút theo demo story; nhờ 2–3 nhóm sinh viên thử tạo/import, tìm My Tasks, cập nhật/gắn evidence và xem presentation; ghi friction.
- **Acceptance criteria:** Có video/link trong README; có ít nhất 3 insight hành động được; tạo Phase 2 backlog từ evidence thay vì cảm tính.
- **Đầu ra:** Video demo, feedback notes và roadmap cập nhật.

## TP-055 — Quyết định dùng Tuần 8 (buffer hay Phase 2 slice)

- **Ưu tiên:** P0 · **Ước lượng:** 0.25 ngày · **Phụ thuộc:** TP-044, TP-050, TP-051
- **Thực hiện:** Rà demo flow, test failures và bug backlog. Dùng Tuần 8 để sửa chất lượng mặc định; chỉ chọn đúng **một** Phase 2 slice nếu M4 đã ổn định và không còn lỗi P0/P1 mở.
- **Acceptance criteria:** Có quyết định ghi lại trong issue/milestone; nếu quality gate chưa qua thì không triển khai webhook/SignalR; nếu qua thì giới hạn slice ở TP-060 hoặc TP-062, không làm cả hai.
- **Đầu ra:** Go/no-go record cho Phase 2 hoặc danh sách hardening đã hoàn tất.

**Gate M5 — Portfolio Ready:** URL demo, repository, video, seed data và README đều hoạt động; có thể kể rõ problem → decision → trade-off → result khi phỏng vấn.

---

# Phase 2 extension — Chỉ dùng cho Tuần 8 khi quality gate đã qua, hoặc sau M5

## TP-060 — GitHub App hoặc repository webhook foundation

- **Trạng thái:** Done — chọn repository webhook.
- **Ưu tiên:** P1 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** M5
- **Thực hiện:** Chọn một integration path, lưu credentials server-side; xác thực `X-Hub-Signature-256`, dedupe `X-GitHub-Delivery`, lưu delivery audit và xử lý async.
- **Acceptance criteria:** Payload giả không qua signature; replay delivery không tạo evidence trùng; API phản hồi nhanh và logs truy vết được delivery.

## TP-061 — Commit/PR evidence automation và leader override

- **Trạng thái:** Done — automation chạy từ repository webhook và giữ manual override của leader.
- **Ưu tiên:** P1 · **Ước lượng:** 1.5 ngày · **Phụ thuộc:** TP-060, TP-030
- **Thực hiện:** Parse `TP-123`; commit chỉ gắn evidence; PR ready → In Review, changes requested → Needs Changes, merged → Done, closed-unmerged → In Progress; leader override luôn ghi activity.
- **Acceptance criteria:** Không bao giờ Done chỉ do commit; malformed code không làm crash webhook; tất cả automation change có audit trail.

## TP-062 — PR Waiting Room và notification nâng cao

- **Trạng thái:** Done — webhook projection, polling UI và GitHub deep-link notifications.
- **Ưu tiên:** P1 · **Ước lượng:** 1.25 ngày · **Phụ thuộc:** TP-061
- **Thực hiện:** Danh sách PR waiting/changes/merged, task liên quan, reviewer, checks, age; inbox/deep link sang GitHub; cân nhắc SignalR sau khi polling flow ổn định.
- **Acceptance criteria:** Reviewer nhìn được action cần làm và mở đúng PR; code review/merge vẫn diễn ra trên GitHub; không biến TeamPulse thành GitHub clone.

---

## Thứ tự thực thi tóm tắt

```text
M0 Foundation
  → M1 Identity & Workspace
  → M2 Task workflow
  → M3 Evidence + Dashboard + Resources
  → M4 Excel + Presentation (MVP feature complete)
  → M5 Hardening + Deploy + Portfolio proof
  → Phase 2 GitHub automation (optional)
```

## Điều kiện cắt scope

Nếu chậm tiến độ, cắt theo thứ tự này trước khi ảnh hưởng core:

1. Drag-and-drop Kanban, dependency UI phức tạp và contribution visualization nâng cao.
2. Notification inbox đầy đủ, realtime SignalR và GitHub automation.
3. Multi-assignee, advisor sharing nâng cao, analytics và Drive API.

Không cắt: workspace isolation, task lifecycle, activity log, evidence/review, Excel confirm transaction, dashboard cơ bản, presentation demo, tests/README/deployment.
