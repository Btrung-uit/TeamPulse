---
id: TP-DOM-009
type: concept
status: accepted
area: domain
tags: [teampulse/domain, resource]
created: 2026-10-14
updated: 2026-10-18
---

# Resource

Resource là metadata và link tới tài liệu hoặc công cụ của project. Resource có thể được liên kết với task nhưng không tự trở thành Evidence cho đến khi người dùng hoặc workflow gắn nó làm bằng chứng.

## MVP rules

- Thuộc đúng một workspace/project và lưu `title`, `type`, URL HTTP(S), optional external ID, owner, optional linked task cùng timestamps.
- Type hỗ trợ Google Docs, Google Drive, Canva, GitHub, Demo và Other; nội dung gốc luôn do external service sở hữu.
- Owner phải là active workspace member; linked task phải thuộc cùng project.
- Mọi active member đọc được; chỉ Leader thêm, sửa hoặc xóa metadata.
- Xóa Resource không xóa external content; liên kết Resource với task không tạo `TaskEvidence`.

Related: [[External Resource Links]], [[External Content Ownership]].
