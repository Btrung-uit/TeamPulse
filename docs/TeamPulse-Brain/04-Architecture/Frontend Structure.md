---
id: TP-ARCH-004
type: architecture
status: accepted
area: architecture
tags: [teampulse/architecture, frontend]
created: 2026-10-14
updated: 2026-10-17
---

# Frontend Structure

Frontend tổ chức theo feature, với `app` cho shell/providers/routing và `components/ui` cho design-system primitives. Component/type riêng của feature ở lại trong feature; chỉ abstraction thực sự dùng chung mới chuyển vào shared folders.

`features/tasks` là vertical slice đầu tiên dùng TanStack Query: `taskApi` giữ HTTP contract, `taskQueries` giữ query keys/list/options/create mutation, còn `pages/TaskBoardPage` chỉ compose trạng thái và UI. Mutation invalidate prefix cache của đúng workspace/project; filter object là một phần query key nên deep link khác nhau không dùng nhầm dữ liệu.

Related: [[Component System]], [[Repository Structure]].
