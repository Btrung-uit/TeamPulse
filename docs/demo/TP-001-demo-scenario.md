# TP-001 — Demo Scenario and MVP Scope Lock

## Scenario

**Project:** CampusEvent — Student Event Management

Một nhóm 4 sinh viên xây web quản lý sự kiện câu lạc bộ. Đây là dữ liệu giả lập để demo TeamPulse, không phải sản phẩm thứ hai cần triển khai.

| Member | Role in TeamPulse | Demo email | Focus |
| --- | --- | --- | --- |
| Bảo Trung | Leader | bao.trung@teampulse.demo | Planning, review, presentation |
| Minh Anh | Member | minh.anh@teampulse.demo | React UI |
| Gia Huy | Member | gia.huy@teampulse.demo | ASP.NET Core API |
| Thu Hà | Member | thu.ha@teampulse.demo | Database, QA, documentation |

The workbook contains 20 task rows across planning, UI, API, database, QA and documentation. It intentionally includes Todo, In Progress, Evidence Linked, In Review, Blocked and Done states so every primary dashboard state has data.

## Locked MVP

The demonstration must use real TeamPulse data and cover:

1. Leader logs in, opens the CampusEvent project and identifies overdue/blocked work from the dashboard.
2. Leader opens the task board, assigns or updates a task, and opens Task Detail.
3. Member opens My Tasks, changes a permitted task state, and attaches a GitHub/document/design/demo URL as evidence.
4. Leader reviews the evidence and accepts work or requests changes. A commit/evidence link alone never automatically completes a task.
5. Leader shows Resource Hub, exports/imports the task workbook, then opens Presentation Mode for an advisor.

## Five-minute demo script

| Time | Action | What it proves |
| --- | --- | --- |
| 0:00–0:35 | Sign in as leader and select CampusEvent. | Auth, workspace/project context and role-aware navigation. |
| 0:35–1:20 | Read Dashboard KPI cards; open a blocked or due-soon task. | Progress/risk visibility from live data. |
| 1:20–2:15 | Open Task Board and Task Detail; assign a task or change its deadline. | Work planning, permission checks and activity logging. |
| 2:15–3:05 | Sign in as a member; open My Tasks and attach evidence to `CE-112`. | Member workflow and evidence without treating commit count as quality. |
| 3:05–3:45 | Return as leader; move `CE-112` through review or request changes. | Evidence → Review → Done lifecycle. |
| 3:45–4:25 | Open Resource Hub and show Docs/Drive/Canva/GitHub links. | TeamPulse coordinates external tools rather than cloning them. |
| 4:25–5:00 | Open Presentation Mode; show contribution evidence and project summary. | Advisor-ready project narrative. |

## MVP acceptance gate

MVP is ready to deploy only when a fresh environment can complete the five-minute script with seeded data and all of the following are true:

- Workspace isolation and role restrictions are enforced.
- Task lifecycle, activity log, evidence and review use persisted backend data.
- Excel import validates before transactional confirm; export opens correctly in Excel/Google Sheets.
- Dashboard, Resource Hub and Presentation Mode reflect the same project data.
- Loading, empty, validation, API error and no-permission states are present on applicable screens.
- E2E smoke test and production-like deployment pass without committed secrets.

## Explicitly outside this MVP

- GitHub App/webhooks, automatic PR status transitions and PR Waiting Room.
- SignalR/realtime and advanced notification delivery.
- Google Drive/Docs/Canva APIs or in-app editors.
- Multi-assignee, advanced analytics and automated contribution scoring.

These remain Phase 2 candidates after real student-team validation.
