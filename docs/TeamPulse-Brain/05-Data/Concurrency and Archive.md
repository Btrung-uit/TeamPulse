---
id: TP-DATA-004
type: rule
status: accepted
area: data
tags: [teampulse/data, concurrency]
created: 2026-10-14
updated: 2026-10-16
---

# Concurrency and Archive

Mutable aggregate như Task dùng optimistic concurrency (`RowVersion`) để không âm thầm ghi đè thay đổi. Task đã có activity, evidence hoặc review được archive bằng timestamp/status; hard-delete chỉ dành cho dữ liệu chưa từng tham gia workflow và cần policy riêng.

`ProjectTask` lưu `HasStartedWorkflow` vĩnh viễn sau lần đầu rời bản nháp `Todo`, kể cả khi task quay về `Todo`. `CanBeHardDeleted` chỉ trả true khi task chưa archive, chưa từng vào workflow và application xác nhận không có activity/evidence/review liên quan. API của `TP-022` phải truyền điều kiện history này và trả concurrency conflict rõ ràng khi `RowVersion` cũ.

Related: [[Activity Log]], [[API Conventions]], [[Task Data Integrity]].
