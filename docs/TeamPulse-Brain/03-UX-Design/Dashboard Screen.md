---
id: TP-UX-010
type: screen
status: accepted
area: ux
tags: [teampulse/ux, screen]
created: 2026-10-14
updated: 2026-10-17
---

# Dashboard Screen

Dashboard trả lời trong vài giây: project đang ở đâu, rủi ro gần nhất là gì và user cần làm gì tiếp theo.

Anatomy: page header/project context → KPI cards (progress, open, review, blocked/due soon) → status breakdown → workload → My Priority Tasks → Recent Activity → Contribution Summary. Mobile ưu tiên progress, due/blocked và tasks cá nhân; bảng contribution cuộn ngang thay vì ép cột.

## Data và interaction

- Dashboard lấy snapshot theo đúng `workspaceId/projectId`; project ngoài tenant không được trộn vào báo cáo.
- KPI là deep link: open → task chưa Done, review → `InReview`, blocked/due soon → danh sách attention trong 7 ngày.
- Contribution hiển thị riêng accepted tasks, evidence trên task Done và review actions. Không cộng thành quality score và không xếp hạng bằng số commit.
- Loading dùng skeleton; project chưa có task dùng first-use state; lỗi API giữ nút retry.

Related: [[Application Shell]], [[System UI States]], [[Mobile Product Scope]].
