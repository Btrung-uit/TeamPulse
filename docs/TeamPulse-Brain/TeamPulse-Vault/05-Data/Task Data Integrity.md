---
id: TP-DATA-003
type: rule
status: accepted
area: data
tags: [teampulse/data, integrity]
created: 2026-10-14
updated: 2026-10-14
---

# Task Data Integrity

Task Code unique trong Project. Assignee phải là active member của owning Workspace; Sprint và dependency phải thuộc cùng Project. Dependency cycle phải bị từ chối. Status transition phải đi qua domain rule thay vì update chuỗi tùy ý.

Related: [[Task Identity]], [[Task Lifecycle]], [[Task Dependency]].
