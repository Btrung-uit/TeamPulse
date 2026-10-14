---
id: TP-ARCH-005
type: architecture
status: accepted
area: architecture
tags: [teampulse/architecture, security]
created: 2026-10-14
updated: 2026-10-14
---

# Identity and Authorization

ASP.NET Core Identity quản lý identity, còn JWT cung cấp API session. Authorization luôn kiểm tra Workspace Membership và resource ownership; role trong token không đủ để truy cập dữ liệu workspace khác.

OAuth token của integration chỉ tồn tại phía server và được mã hóa khi lưu.

Related: [[Workspace and Membership]], [[Workspace Data Ownership]], [[GitHub Webhook Security]].
