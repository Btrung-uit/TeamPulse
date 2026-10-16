---
id: TP-ARCH-005
type: architecture
status: accepted
area: architecture
tags: [teampulse/architecture, security]
created: 2026-10-14
updated: 2026-10-16
---

# Identity and Authorization

ASP.NET Core Identity quản lý identity, còn JWT cung cấp API session. Authorization luôn kiểm tra Workspace Membership và resource ownership; role trong token không đủ để truy cập dữ liệu workspace khác.

Route mang `workspaceId` dùng policy `WorkspaceMember` hoặc `WorkspaceLeader`. Authorization handler đọc active membership bằng query cross-scope có chủ đích, sau đó mới thiết lập `WorkspaceScope` để EF Core query filters cho phép truy vấn tenant hiện tại.

Project endpoints kế thừa hai policy này. Mọi active workspace role được list/get project trong workspace; create/update/archive yêu cầu `WorkspaceLeader`. `projectId` vẫn được truy vấn cùng `workspaceId`, nên ID của project thuộc tenant khác trả not found sau khi workspace authorization thành công và không thể làm rò rỉ resource chéo tenant.

MVP dùng access token JWT 15 phút chỉ lưu trong memory của frontend. Refresh token opaque 7 ngày nằm trong cookie `HttpOnly`, `SameSite=Strict`; database chỉ lưu SHA-256 hash. Refresh token được rotate ở mỗi lần refresh và revoke khi logout. JWT không chứa workspace role.

Canonical decision: `docs/adr/0005-use-short-lived-jwt-and-rotating-refresh-sessions.md`.

OAuth token của integration chỉ tồn tại phía server và được mã hóa khi lưu.

Related: [[Workspace and Membership]], [[Workspace Data Ownership]], [[GitHub Webhook Security]].
