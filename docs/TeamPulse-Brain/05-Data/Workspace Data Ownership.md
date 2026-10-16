---
id: TP-DATA-001
type: rule
status: accepted
area: data
tags: [teampulse/data, multitenancy]
created: 2026-10-14
updated: 2026-10-16
---

# Workspace Data Ownership

Workspace là tenant boundary trong shared SQL Server schema. Project thuộc một Workspace; mọi task, evidence, resource và report phải truy ngược được về workspace đó, và mọi query/mutation phải xác thực membership tương ứng.

`TP-014` khóa rule này bằng integration suite hai tenant. Với Workspace B, token của Leader A nhận `403` ngay tại workspace policy. Nếu client đặt `projectId` của B dưới route Workspace A đã được phép, resource query phải trả `404`; API không tiết lộ resource tồn tại ở tenant khác và không được tạo activity/mutation ngoài tenant.

Authorization dùng active membership hiện tại trong database ở mỗi request. JWT chỉ xác nhận identity, không đóng băng workspace role: demote role có hiệu lực ngay, deactivate membership chặn tenant access nhưng không vô hiệu hóa identity endpoint `/api/auth/me`.

Canonical decision: `docs/adr/0002-use-sql-server-shared-schema-multitenancy.md`.

Related: [[Workspace and Membership]], [[Identity and Authorization]].
