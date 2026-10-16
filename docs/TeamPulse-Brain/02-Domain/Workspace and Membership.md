---
id: TP-DOM-001
type: concept
status: accepted
area: domain
tags: [teampulse/domain, workspace]
created: 2026-10-14
updated: 2026-10-16
---

# Workspace and Membership

Workspace là ranh giới cộng tác và sở hữu dữ liệu của một nhóm. Một User có thể thuộc nhiều Workspace thông qua Workspace Membership mang role `Leader`, `Member` hoặc `Advisor`.

Quyền được đánh giá trong workspace hiện tại, không phải role toàn cục trên tài khoản.

`Leader` tạo workspace, thêm user đã đăng ký và thay role. `Member` cộng tác nhưng không tự tăng quyền. `Advisor` chỉ đọc. Hệ thống không cho hạ quyền Leader cuối cùng và ghi mọi membership mutation vào [[Activity Log]].

Related: [[Identity and Authorization]], [[Workspace Data Ownership]].
