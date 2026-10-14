---
id: TP-ARCH-003
type: architecture
status: accepted
area: architecture
tags: [teampulse/architecture, backend]
created: 2026-10-14
updated: 2026-10-14
---

# Backend Boundaries

- **Api:** transport, middleware, authentication entry và composition root.
- **Application:** use cases, validation, authorization policy và DTO.
- **Domain:** entities, value objects, lifecycle rules; không phụ thuộc infrastructure.
- **Infrastructure:** EF Core, Identity persistence, Excel, GitHub và external adapters.

Related: [[Modular Monolith]], [[API Conventions]].
