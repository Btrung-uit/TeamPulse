---
id: TP-INT-004
type: feature
status: accepted
area: integrations
tags: [teampulse/integrations, git]
created: 2026-10-14
updated: 2026-10-18
---

# Git Command Generator

Generator nằm trong Task Detail trên desktop và tạo một sequence Git an toàn, gắn với task hiện tại. Người dùng có thể chỉnh:

- branch type: `feature`, `fix`, `docs`, `test`, `refactor`, `chore`;
- commit type: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`;
- commit summary;
- từng đường dẫn file cần stage.

Branch có dạng `<type>/<task-code>-<task-title-slug>` và commit có dạng `<type>: <summary> #<TASK-CODE>`. Sequence gồm đồng bộ `main` bằng fast-forward, tạo task branch, kiểm tra working tree, stage các path cụ thể, xem staged diff, commit và push task branch. Mỗi command có nút copy riêng; `Copy all` chỉ được bật khi có ít nhất một path hợp lệ.

Generator không thực thi command. Nó không tạo `git add .`, không nhận absolute path, path traversal, wildcard hoặc shell metacharacter, và lệnh push không bao giờ nhắm trực tiếp tới `main`. Người dùng vẫn phải đọc `git status` và `git diff --cached` trước khi commit.

Related: [[Task Identity]], [[MVP Scope]], [[Git Workflow]].
