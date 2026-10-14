---
id: TP-ARCH-004
type: architecture
status: accepted
area: architecture
tags: [teampulse/architecture, frontend]
created: 2026-10-14
updated: 2026-10-14
---

# Frontend Structure

Frontend tổ chức theo feature, với `app` cho shell/providers/routing và `components/ui` cho design-system primitives. Component/type riêng của feature ở lại trong feature; chỉ abstraction thực sự dùng chung mới chuyển vào shared folders.

Related: [[Component System]], [[Repository Structure]].
