---
id: TP-ARCH-007
type: architecture
status: proposed
area: architecture
tags: [teampulse/architecture, realtime]
created: 2026-10-14
updated: 2026-10-14
---

# Realtime Updates

SignalR là delivery mechanism dự kiến cho notification và refresh UI sau thay đổi; database vẫn là source of truth. Client mất kết nối phải reload canonical state thay vì suy đoán event đã nhận đủ.

Realtime thuộc Phase 2 và không chặn MVP.

Related: [[Notification]], [[Delivery Phases]].
