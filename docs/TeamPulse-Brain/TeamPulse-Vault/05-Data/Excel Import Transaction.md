---
id: TP-DATA-005
type: rule
status: accepted
area: data
tags: [teampulse/data, excel]
created: 2026-10-14
updated: 2026-10-14
---

# Excel Import Transaction

Import flow là upload → map → validate/preview → confirm. Chỉ bước confirm mới tạo task và thực hiện trong transaction; all valid rows commit together or none do. File extension phía client không đủ: server kiểm tra size, structure, permission, required fields, enum, member, unique code và dependency.

Related: [[Import Batch]], [[Excel Import Export]], [[Excel Source of Truth Rule]].
