---
id: TP-DEL-003
type: strategy
status: accepted
area: delivery
tags: [teampulse/delivery, testing]
created: 2026-10-14
updated: 2026-10-14
---

# Test Strategy

- **Unit:** task lifecycle, dependency, permissions, contribution rules và import validation.
- **Integration:** API authorization, EF Core constraints, concurrency, workspace isolation và transactional import.
- **Frontend:** component states, form behavior và feature integration.
- **E2E smoke:** leader creates/imports work, member updates task, evidence is visible, presentation opens.

Test tập trung business risk thay vì chỉ tối đa coverage percentage.

Related: [[Definition of Done]], [[Task Data Integrity]], [[Excel Import Transaction]].
