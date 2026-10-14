---
id: TP-DATA-004
type: rule
status: accepted
area: data
tags: [teampulse/data, concurrency]
created: 2026-10-14
updated: 2026-10-14
---

# Concurrency and Archive

Mutable aggregate như Task dùng optimistic concurrency (`RowVersion`) để không âm thầm ghi đè thay đổi. Task đã có activity, evidence hoặc review được archive bằng timestamp/status; hard-delete chỉ dành cho dữ liệu chưa từng tham gia workflow và cần policy riêng.

Related: [[Activity Log]], [[API Conventions]], [[Task Data Integrity]].
