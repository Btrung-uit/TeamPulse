---
id: TP-DOM-002
type: concept
status: accepted
area: domain
tags: [teampulse/domain, project]
created: 2026-10-14
updated: 2026-10-16
---

# Project and Sprint

Project là một body of work thuộc đúng một Workspace. Sprint là khoảng lập kế hoạch có giới hạn thời gian bên trong Project; task có thể chưa thuộc sprint nhưng không thể thuộc sprint của project khác.

Trong MVP, quyền xem Project kế thừa từ active Workspace Membership: `Leader`, `Member` và `Advisor` đều đọc được toàn bộ project trong workspace đang tham gia. Chưa có `ProjectMember` hay project riêng tư. Chỉ `Leader` được tạo, sửa hoặc archive project.

Sprint dùng cùng mô hình quyền: mọi active member đọc được, chỉ `Leader` tạo/sửa/xóa và gán task. Tên Sprint unique không phân biệt hoa thường trong Project; `EndsOn` không được trước `StartsOn`. Sprint chỉ được hard-delete khi không còn task được gán, tránh việc xóa kế hoạch làm task âm thầm mất context. Task có thể được unassign về backlog bằng `SprintId = null`.

Archive là soft archive, không phải delete. Project đã archive vẫn xuất hiện khi client yêu cầu `includeArchived`, giữ nguyên history và không được sửa. Lệnh archive lặp lại là idempotent. Create, update và archive Project; create, update, delete Sprint; và thay đổi sprint assignment đều tạo immutable [[Activity Log]].

Project context canonical trên web nằm trong URL `/workspaces/{workspaceId}/projects/{projectId}` để deep link và refresh khôi phục đúng tenant/resource, thay vì dựa vào state tạm hoặc local storage.

Related: [[Workspace and Membership]], [[Task Identity]].
