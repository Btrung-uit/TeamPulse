---
id: TP-DATA-001
type: rule
status: accepted
area: data
tags: [teampulse/data, multitenancy]
created: 2026-10-14
updated: 2026-10-14
---

# Workspace Data Ownership

Workspace là tenant boundary trong shared SQL Server schema. Project thuộc một Workspace; mọi task, evidence, resource và report phải truy ngược được về workspace đó, và mọi query/mutation phải xác thực membership tương ứng.

Canonical decision: `docs/adr/0002-use-sql-server-shared-schema-multitenancy.md`.

Related: [[Workspace and Membership]], [[Identity and Authorization]].
