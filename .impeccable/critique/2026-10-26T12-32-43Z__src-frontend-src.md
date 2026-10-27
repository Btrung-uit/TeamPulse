---
target: trang web TeamPulse
total_score: 24
max_score: 40
na_heuristics:
p0_count: 0
p1_count: 3
target_identity: "file:E:\\acer\\Documents\\Work\\Personal Project\\TeamPulse\\src\\frontend\\src"
timestamp: 2026-10-26T12-32-43Z
slug: src-frontend-src
closed: true
---
Method: dual-agent (A: impeccable_assessment_a · B: impeccable_assessment_b)

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---:|---|
| 1 | Visibility of System Status | 3/4 | Loading, toast, retry và trạng thái cập nhật tốt; luồng chờ quyền chưa có trạng thái xuyên suốt. |
| 2 | Match System / Real World | 3/4 | Ngôn ngữ hợp nhóm sinh viên, nhưng PR/evidence/lifecycle vẫn cần kiến thức miền. |
| 3 | User Control and Freedom | 2/4 | Có cancel/Esc/confirm, nhưng thiếu undo và sign-out desktop quá trực tiếp. |
| 4 | Consistency and Standards | 3/4 | Component nhất quán; nhãn/đích điều hướng desktop và mobile chưa đồng nhất. |
| 5 | Error Prevention | 3/4 | Validation và confirmation tốt; invite/recovery còn là dead end. |
| 6 | Recognition Rather Than Recall | 3/4 | Context và labels rõ; bảy trạng thái cùng dependency semantics tăng tải ghi nhớ. |
| 7 | Flexibility and Efficiency | 2/4 | Có Ctrl+K, URL filters, import/export; thiếu bulk actions và workflow shortcuts. |
| 8 | Aesthetic and Minimalist Design | 2/4 | Sạch nhưng dashboard dài, board dày control và Development lộ trong production IA. |
| 9 | Error Recovery | 2/4 | Error gần nguồn và có retry; không có forgot-password/recovery path. |
| 10 | Help and Documentation | 1/4 | Có helper copy cục bộ, chưa có help entry point hay giải thích theo ngữ cảnh. |
| **Total** |  | **24/40** | **Acceptable — nền tảng tốt, nhưng core workflow còn tải nhận thức cao.** |

## Design Specificity Verdict

**TeamPulse có bản sắc ở kiến trúc thông tin, chưa đủ bản sắc ở lớp thị giác.** Chuỗi Task → Evidence → Review → Contribution → Presentation là khác biệt thật; Dashboard và Presentation Mode phản ánh đúng product truth. Tuy nhiên shell sidebar/topbar/card bo góc, Lucide icon và typography system sans vẫn có thể đổi nhãn để trở thành một SaaS quản lý dự án khác. Presentation Mode là bề mặt đặc trưng nhất nhưng ngôn ngữ kể chuyện của nó chưa lan sang shell chính.

Detector quét 66 tệp markup và chỉ trả **1 warning**: rule `side-tab` tại `src/frontend/src/styles/presentation.css:370` cho `border-left: 3px solid #8fa7ff`. Đây nhiều khả năng là false positive/mislabeled vì selector là `.presentation-footnote`, không phải card; không nên sửa chỉ để làm detector sạch.

Không có visual overlay đáng tin cậy: preflight mutation thành công, nhưng Chromium chặn trang HTTPS tải detector từ HTTP localhost bởi Private Network Access/CORS.

## Overall Impression

TeamPulse đã vượt mức “đồ án CRUD” ở state design, accessibility và mô hình nghiệp vụ. Cơ hội lớn nhất không phải thêm hiệu ứng hay đổi palette, mà là giảm số quyết định trên Task Board và làm rõ TeamPulse trên mobile là công cụ làm việc hay chỉ để theo dõi.

## What's Working

1. **IA bám product truth:** Dashboard và Presentation Mode kể đúng chuỗi công việc → bằng chứng → review → contribution, không biến số commit thành điểm chất lượng.
2. **State/error design có kỷ luật:** skeleton, empty/filtered-empty state, retry, validation gần field, preserved filters và toast đều hiện diện.
3. **Accessibility foundation tốt:** skip link, focus-visible, modal focus trap/restore, Esc, `aria-live`, label và target 44px đã được xây dựng có chủ đích.

## Priority Issues

### [P1] Mobile IA che giấu planning/admin của Leader

**Why it matters:** sidebar biến mất dưới 767px; bottom nav chỉ có Home, Tasks, Inbox, More, còn More không dẫn tới Task Board, Members hay Projects. Responsive CSS vẫn hỗ trợ các bề mặt này, nên navigation và capability gửi hai thông điệp trái nhau.

**Fix:** quyết định rõ mobile hỗ trợ quản trị hay chỉ monitoring. Nếu hỗ trợ, thêm các đích role-aware vào More. Nếu không, nói rõ giới hạn và đưa CTA “Open on desktop” ở đúng nơi người dùng tìm planning action.

**Suggested command:** `$impeccable adapt`

### [P1] Task Board vượt ngưỡng tải nhận thức

**Why it matters:** ba header actions, năm filter/sort và bảy lifecycle columns cùng xuất hiện; mobile dùng bảy cột rộng gần viewport, khiến so sánh bottleneck thành chuỗi swipe dài.

**Fix:** giữ Search + một Filter trigger, đưa filter nâng cao vào popover/drawer và hiện active chips. Cho chọn My work / Needs attention / Full workflow; mobile mặc định dùng attention list thay vì mini-kanban bảy cột.

**Suggested command:** `$impeccable distill`

### [P1] Join-workspace tạo dead end ngoài sản phẩm

**Why it matters:** người dùng phải copy email, nhắn Leader ngoài TeamPulse, quay lại và tự “Check workspace access”; không có pending status, invite state hay xác nhận người cần hành động.

**Fix:** tối thiểu thêm invitation link/token hoặc pending request có trạng thái. Nếu backend chưa có invite, vẫn cần pending screen bền vững thay cho vòng lặp copy → nhắn ngoài app → check.

**Suggested command:** `$impeccable onboard`

### [P2] Production navigation lộ UI components

**Why it matters:** Development → UI components xuất hiện như destination cấp một, làm bản public giống showcase nội bộ và cạnh tranh với Presentation Mode.

**Fix:** ẩn route/nav bằng development flag hoặc quyền nội bộ; không cần redesign.

**Suggested command:** `$impeccable polish`

### [P2] Account recovery và sign-out chưa đạt chuẩn public app

**Why it matters:** login sai có thông báo rõ nhưng không có forgot-password/support path. User control desktop trông như menu profile nhưng logout ngay một click.

**Fix:** thêm recovery path tối thiểu; đổi user control thành menu Profile/Sign out hoặc thêm xác nhận nếu giữ hành vi trực tiếp.

**Suggested command:** `$impeccable harden`

## Cognitive Load

**4/8 checklist failures → high cognitive load trên core workflow.** Fail ở single focus, one thing at a time, minimal choices và progressive disclosure. Task-detail panel gom metadata, status, dependency, evidence, review và archive; Task Board là decision point vượt ngưỡng bốn lựa chọn. Grouping, chunking, hierarchy và giữ context hiện làm khá tốt.

## Emotional Journey

- Auth mở đầu điềm tĩnh, mobile form gọn và CTA đủ 44px.
- Valley lớn nhất là join-workspace phụ thuộc nhắn ngoài sản phẩm rồi quay lại kiểm tra thủ công.
- Peak là Presentation Mode: outcome, progress, contribution và resources tạo narrative rõ.
- Copy xác nhận deadline/dependency/archive tạo reassurance tốt ở hành động rủi ro.
- Kết thúc yếu ở password recovery và mobile Leader không tìm thấy planning/admin.

## Persona Red Flags

**Alex — Power User:** không có shortcut tạo task/chuyển status, không có bulk update, board phải cuộn ngang và detail panel quá dài cho thao tác nhanh.

**Sam — Accessibility-dependent:** nền semantic tốt, nhưng board là vùng focusable không có hướng dẫn điều hướng bảy cột; cuộn ngang lồng cuộn dọc dễ mất vị trí ở zoom 200%; thiếu help theo ngữ cảnh cho evidence/dependency/transitions.

**Casey — Distracted Mobile User:** bottom nav thuận tay, nhưng planning không reachable; deep link tới board dẫn vào filter dài + bảy cột swipe; join workspace bắt chuyển app đúng điểm dễ bỏ dở.

## Minor Observations

- Sidebar có sáu group heading, nhiều group chỉ có một item nên tăng chiều dài hơn giá trị định hướng.
- Projects và Dashboard cùng dùng `LayoutDashboard`, làm giảm recognition.
- Contribution summary quan trọng nhưng nằm quá sâu dưới fold trên dashboard dài.
- Mobile “Tasks” thực chất mở My Tasks, trong khi desktop phân biệt Task board và My tasks.
- Detector warning duy nhất có độ tin cậy thấp; ưu tiên sửa UX thực tế trước.

## Questions to Consider

- Mobile là công cụ làm việc hay chỉ monitoring? Hiện responsive capability và navigation trả lời khác nhau.
- TeamPulse có cần cho mọi người nhìn đủ bảy trạng thái cùng lúc, hay chỉ cần My work, Needs attention và Full workflow khi chủ động mở?
- Màn hình nào phải chứng minh TeamPulse khác Trello/Jira: Dashboard hiện tại hay một Contribution story gần Presentation Mode hơn?
