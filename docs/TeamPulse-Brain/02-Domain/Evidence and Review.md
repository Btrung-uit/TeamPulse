---
id: TP-DOM-006
type: concept
status: accepted
area: domain
tags: [teampulse/domain, evidence]
created: 2026-10-14
updated: 2026-10-17
---

# Evidence and Review

Evidence là reference chứng minh công việc liên quan một task: commit, PR, issue, document, design hoặc demo. Review là đánh giá có chủ đích trước khi chấp nhận công việc; presence of evidence không tự đồng nghĩa với Done.

`TP-030` lưu evidence dưới dạng metadata (`Type`, URL HTTP/HTTPS, note, submitter, timestamps), không sao chép nội dung bên ngoài vào TeamPulse. Assignee của task hoặc Leader được gắn evidence; evidence đầu tiên gắn khi task đang `In Progress` đưa task sang `Evidence Linked`, nhưng không bao giờ tự đưa task sang `Done`.

Review thủ công chỉ được ghi khi task đang `In Review` và dùng `RowVersion` để tránh quyết định trên dữ liệu cũ. Leader chọn `Accept` để chuyển sang `Done` hoặc `Request changes` để chuyển sang `Needs Changes`; comment và activity tương ứng được lưu trong cùng transaction với status transition. `TaskComment` trong MVP chỉ giữ feedback gắn với quyết định review, chưa phải hệ thống chat tổng quát.

API surface: `GET .../tasks/{taskId}/collaboration`, `POST .../evidence`, `POST .../review`. Mọi truy vấn vẫn đi qua workspace query filter và composite task ownership.

Related: [[Core Value Chain]], [[Contribution Profile]], [[PR Waiting Room]].
