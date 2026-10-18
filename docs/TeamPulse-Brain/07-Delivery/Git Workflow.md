---
id: TP-DEL-004
type: process
status: accepted
area: delivery
tags: [teampulse/delivery, git]
created: 2026-10-14
updated: 2026-10-18
---

# Git Workflow

Feature work dùng short-lived branch và Pull Request. `main` luôn deployable; branch protection yêu cầu checks/review khi repository có collaborator. Commit dùng Conventional Commits và tham chiếu Task Code khi liên quan.

Không commit secrets, generated artifacts, Excel lock files hoặc dùng `git add .` thiếu kiểm tra.

Git Command Generator hỗ trợ workflow nhưng không thay người dùng thực thi hay review. Sequence chuẩn là: chuyển sang `main`, `pull --ff-only`, tạo branch theo Task Code, xem `status`, stage explicit paths với `--`, xem staged diff, commit theo Conventional Commits có `#TaskCode`, rồi push task branch để mở PR. Nếu path chưa rõ hoặc không hợp lệ, generator không sinh lệnh stage và không cho copy toàn bộ sequence.

Related: [[Git Command Generator]], [[PR Waiting Room]].
