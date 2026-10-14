---
id: TP-ARCH-002
type: decision
status: accepted
area: architecture
tags: [teampulse/architecture, decision]
created: 2026-10-14
updated: 2026-10-14
---

# Modular Monolith

TeamPulse là một deployable ASP.NET Core application với boundary rõ giữa API, Application, Domain và Infrastructure. Microservices bị loại khỏi MVP vì tăng chi phí deployment, networking, observability và consistency mà chưa tạo giá trị người dùng tương ứng.

Canonical decision: `docs/adr/0001-use-modular-monolith.md`.

Related: [[Backend Boundaries]], [[Repository Structure]].
