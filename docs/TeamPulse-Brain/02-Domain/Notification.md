---
id: TP-DOM-010
type: concept
status: accepted
area: domain
tags: [teampulse/domain, notification]
created: 2026-10-14
updated: 2026-10-21
---

# Notification

Notification là alert hướng tới một user khi có sự kiện cần biết hoặc cần hành động: assignment, deadline, mention, review request hoặc PR state. TeamPulse không gửi notification cho mọi activity nhỏ.

MVP đã lưu notification in-app theo workspace cho assignment, review request, review accepted và changes requested. Read receipt thuộc từng recipient; query và mutation đều đi qua workspace membership boundary. Email, mention parsing, deadline scheduler và realtime SignalR vẫn thuộc milestone sau.

Related: [[Activity Log]], [[Realtime Updates]], [[Mobile Product Scope]].
