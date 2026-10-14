---
id: TP-DEL-004
type: process
status: accepted
area: delivery
tags: [teampulse/delivery, git]
created: 2026-10-14
updated: 2026-10-14
---

# Git Workflow

Feature work dùng short-lived branch và Pull Request. `main` luôn deployable; branch protection yêu cầu checks/review khi repository có collaborator. Commit dùng Conventional Commits và tham chiếu Task Code khi liên quan.

Không commit secrets, generated artifacts, Excel lock files hoặc dùng `git add .` thiếu kiểm tra.

Related: [[Git Command Generator]], [[PR Waiting Room]].
