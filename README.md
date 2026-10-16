# TeamPulse

> **VI:** Trung tâm điều phối đồ án dành cho nhóm sinh viên: từ task, deadline và tài nguyên đến GitHub evidence, review, contribution report và presentation.
>
> **EN:** A project coordination hub for student teams—from tasks, deadlines, and resources to GitHub evidence, reviews, contribution reports, and presentations.

![Status](https://img.shields.io/badge/status-sprint%20planning-C9A24A)
[![CI](https://github.com/Btrung-uit/TeamPulse/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Btrung-uit/TeamPulse/actions/workflows/ci.yml)
![Backend](https://img.shields.io/badge/backend-ASP.NET%20Core-512BD4)
![Frontend](https://img.shields.io/badge/frontend-React%20%2B%20TypeScript-3178C6)
![Database](https://img.shields.io/badge/database-SQL%20Server-CC2927)

**Language / Ngôn ngữ:** [Tiếng Việt](#tiếng-việt) · [English](#english)

---

# Tiếng Việt

## Tổng quan

TeamPulse là một **Student Project & Engineering Collaboration Hub**. Sản phẩm tập trung giải quyết sự phân mảnh khi nhóm sinh viên phải chuyển đổi giữa bảng task, Google Docs, Google Drive, Canva, GitHub và ứng dụng nhắn tin để theo dõi một đồ án.

TeamPulse không thay thế các công cụ chuyên dụng đó. Nó trở thành nguồn sự thật cho tiến độ dự án và liên kết công việc theo chuỗi:

```text
Task → Evidence → Review → Contribution Report → Presentation
```

## Đối tượng sử dụng

- **Leader:** lập kế hoạch, phân công, theo dõi rủi ro và xác nhận đóng góp.
- **Member:** theo dõi công việc cá nhân, cập nhật tiến độ và gắn minh chứng.
- **Advisor:** theo dõi tiến độ và xem báo cáo mà không thay đổi dữ liệu vận hành.

## Tính năng chính

| Nhóm | Khả năng |
|---|---|
| Workspace & Project | Tạo nhóm, project, thành viên và quyền truy cập theo workspace |
| Work Management | Sprint, task, deadline, priority, dependency, Kanban và activity log |
| Dashboard | Tiến độ, workload, task trễ hạn/blocked và hoạt động gần đây |
| Evidence | Liên kết commit, pull request, issue, tài liệu, thiết kế và demo với task |
| Excel Onboarding | Upload → map cột → validate/preview → xác nhận import; export báo cáo `.xlsx` |
| Resources | Quản lý metadata/link của Google Docs, Drive, Canva, GitHub và bản demo |
| Presentation Mode | Tóm tắt tiến độ, thành viên, contribution, tài nguyên và demo cho giảng viên |
| GitHub Collaboration | PR Waiting Room, webhook, tự gắn evidence và chuyển trạng thái theo PR workflow |
| Notifications | Task mới, deadline, comment, review request và trạng thái PR |
| Mobile Monitoring | Theo dõi tiến độ, My Tasks, Inbox, PR và Resources với thao tác rủi ro thấp |

## Quy tắc workflow quan trọng

```text
Todo → In Progress → Evidence Linked → In Review → Done
```

- Commit chứa mã như `TP-123` chỉ gắn evidence; không tự hoàn thành task.
- PR mở hoặc ready for review đưa task sang `In Review`.
- Review yêu cầu sửa đưa task sang `Needs Changes`.
- Chỉ PR được merge vào nhánh đích hoặc leader xác nhận mới đưa task sang `Done`.
- Task đã có lịch sử/evidence được archive thay vì hard-delete.

## Nền tảng kỹ thuật dự kiến

| Phần | Công nghệ |
|---|---|
| Frontend | React, TypeScript |
| Backend | ASP.NET Core Web API |
| Persistence | SQL Server, Entity Framework Core |
| Authentication | ASP.NET Core Identity, JWT |
| Realtime | SignalR |
| Excel | ClosedXML |
| Integration | GitHub App, REST API, Webhooks |
| Delivery | Docker Compose, GitHub Actions |
| Architecture | Modular monolith trong monorepo |

## Kiến trúc cấp cao

```mermaid
flowchart LR
    Web[React + TypeScript] -->|REST / SignalR| API[ASP.NET Core API]
    API --> App[Application]
    App --> Domain[Domain]
    App --> Infra[Infrastructure]
    Infra --> DB[(SQL Server)]
    Infra --> GitHub[GitHub]
    Infra --> External[Docs / Drive / Canva links]
```

## Cấu trúc repository

```text
TeamPulse/
├── .github/workflows/              # CI/CD
├── src/
│   ├── backend/
│   │   ├── TeamPulse.Api/
│   │   ├── TeamPulse.Application/
│   │   ├── TeamPulse.Domain/
│   │   └── TeamPulse.Infrastructure/
│   └── frontend/
│       ├── public/
│       └── src/
│           ├── app/
│           ├── components/ui/
│           ├── features/
│           ├── hooks/
│           ├── lib/
│           ├── pages/
│           ├── styles/
│           └── types/
├── tests/
│   ├── backend/
│   │   ├── TeamPulse.UnitTests/
│   │   └── TeamPulse.IntegrationTests/
│   ├── frontend/
│   └── e2e/
├── database/
│   ├── init/
│   └── seed/
├── docs/
│   ├── adr/
│   ├── api/
│   ├── design/
│   ├── diagrams/
│   └── TeamPulse-Brain/
├── scripts/
├── .env.example
├── docker-compose.yml
├── TeamPulse.sln
├── global.json
├── CONTEXT.md
└── README.md
```

## Phân kỳ

### MVP

Auth, workspace, project, role, task/sprint, dashboard, activity log, evidence link, Excel import/export, Resource Hub, Presentation Mode và Git Command Generator.

### Phase 2

GitHub App/Webhook, PR Waiting Room, tự động hóa trạng thái, SignalR và notification nâng cao.

### Phase 3

Google Drive API, dependency visualization, advisor dashboard và analytics nâng cao.

## Thiết kế giao diện

- **Dark:** Obsidian Black + Muted Gold.
- **Light:** Porcelain White + Cool Silver.
- Data-first, bo tròn có kiểm soát, chuyển động ngắn và có mục đích.
- Desktop-first; mobile tập trung theo dõi và thao tác nhanh có rủi ro thấp.
- Trạng thái nghiệp vụ luôn có text/icon, không chỉ biểu diễn bằng màu.

## Chạy local

Yêu cầu: Docker Desktop (hoặc Docker Engine với Compose v2), .NET SDK 10.0.401 trở lên và Node.js `^20.19.0` hoặc `>=22.12.0`.

Thiết lập SQL Server lần đầu:

```powershell
Copy-Item .env.example .env
# Đổi MSSQL_SA_PASSWORD trong .env trước khi sử dụng lâu dài.
docker compose up -d

# Dùng cùng password vừa đặt trong .env; secret được lưu ngoài repository.
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "ConnectionStrings:TeamPulse" `
  "Server=localhost,1433;Database=TeamPulse;User Id=sa;Password=<your-password>;TrustServerCertificate=True"

# Tạo JWT signing key ngẫu nhiên; chỉ lưu bằng User Secrets.
$jwtSigningKey = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "Authentication:Jwt:SigningKey" $jwtSigningKey

# Cài local tool và áp dụng EF Core migrations vào database trống/hiện có.
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/backend/TeamPulse.Infrastructure `
  --startup-project src/backend/TeamPulse.Api
```

Những lần sau, một lệnh `docker compose up -d` sẽ khởi động SQL Server và bảo đảm database `TeamPulse` tồn tại.

```powershell
# Backend: restore, build và test
dotnet restore TeamPulse.sln
dotnet build TeamPulse.sln --no-restore
dotnet test TeamPulse.sln --no-build

# Chạy API tại http://localhost:5249
dotnet run --project src/backend/TeamPulse.Api
```

Trong terminal khác:

```powershell
cd src/frontend
npm ci
npm run dev
```

Các health endpoint:

- `GET http://localhost:5249/health` hoặc `/health/live`: API đang chạy.
- `GET http://localhost:5249/health/ready`: API kết nối được SQL Server.

Kiểm tra production build của frontend bằng `npm run build`. Dừng hạ tầng local bằng `docker compose down`; named volume giữ lại dữ liệu giữa các lần chạy.

## Continuous Integration

Workflow `CI` chạy trên mọi pull request vào `main`, mọi push lên `main` và khi kích hoạt thủ công. Ba check ổn định là:

- `Backend`: restore, kiểm tra format, build Release và chạy test .NET.
- `Frontend`: `npm ci`, lint, kiểm tra format, component tests và production build.
- `CI Gate`: chỉ thành công khi cả hai job trên đều thành công.

Workflow chỉ có quyền đọc source code và không sử dụng application secret. Khi repository có collaborator, hãy đặt `CI Gate` làm required status check của `main`.

## Trạng thái dự án

TP-021 bổ sung planning API cho Sprint, gán task vào sprint và dependency graph. Hệ thống chặn self-dependency, dependency chéo project và cycle gián tiếp; successor nhận cảnh báo predecessor chưa xong/quá hạn hoặc deadline xung đột nhưng TeamPulse không tự thay đổi lịch. `Leader` quản trị planning, còn `Member`/`Advisor` chỉ đọc.

## Project Brain

Các quyết định, quy tắc domain, thiết kế, kiến trúc, tích hợp và kế hoạch delivery được chia thành atomic notes tại [`docs/TeamPulse-Brain`](docs/TeamPulse-Brain/00-Home/Home.md).

`CONTEXT.md` là glossary chuẩn cho code. `docs/adr/` là nơi lưu các quyết định kiến trúc khó đảo ngược.

---

# English

## Overview

TeamPulse is a **Student Project & Engineering Collaboration Hub** designed to reduce the fragmentation created by switching between task spreadsheets, Google Docs, Google Drive, Canva, GitHub, and messaging apps.

It does not replace those specialist tools. TeamPulse is the source of truth for project progress and connects work through:

```text
Task → Evidence → Review → Contribution Report → Presentation
```

## Intended users

- **Leader:** plans work, assigns ownership, monitors risks, and confirms contributions.
- **Member:** tracks personal tasks, reports progress, and attaches evidence.
- **Advisor:** observes progress and reports without changing operational data.

## Planned capabilities

- Workspace, projects, membership, and workspace-scoped authorization.
- Sprint, task, deadline, dependency, Kanban, and immutable activity history.
- Progress, workload, overdue, blocked, and contribution dashboards.
- Task evidence linked to commits, pull requests, issues, documents, designs, and demos.
- Transactional Excel import with mapping and preview, plus `.xlsx` export.
- Resource links for Google Docs, Drive, Canva, GitHub, and deployed demos.
- Presentation Mode for project demonstrations and assessment.
- GitHub PR Waiting Room, webhook automation, and evidence-driven task transitions.
- Monitoring-first mobile experience for Home, My Tasks, Inbox, PRs, and Resources.

## Planned stack

React + TypeScript, ASP.NET Core Web API, SQL Server, Entity Framework Core, ASP.NET Core Identity, JWT, SignalR, ClosedXML, Docker Compose, and GitHub Actions in a modular monolith monorepo.

## Delivery phases

- **MVP:** core identity, work management, dashboards, activity/evidence, Excel onboarding, resource links, presentation, and Git command guidance.
- **Phase 2:** GitHub App/webhooks, PR Waiting Room, workflow automation, realtime updates, and richer notifications.
- **Phase 3:** Drive integration, dependency visualization, advisor views, and advanced analytics.

## Run locally

Requirements: Docker Desktop (or Docker Engine with Compose v2), .NET SDK 10.0.401 or newer, and Node.js `^20.19.0` or `>=22.12.0`.

Set up SQL Server for the first time:

```powershell
Copy-Item .env.example .env
# Change MSSQL_SA_PASSWORD in .env before long-term use.
docker compose up -d

# Use the same password configured in .env; the secret is stored outside the repository.
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "ConnectionStrings:TeamPulse" `
  "Server=localhost,1433;Database=TeamPulse;User Id=sa;Password=<your-password>;TrustServerCertificate=True"

# Generate a random JWT signing key and keep it in User Secrets only.
$jwtSigningKey = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "Authentication:Jwt:SigningKey" $jwtSigningKey

# Restore the local tool and apply EF Core migrations to an empty or existing database.
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/backend/TeamPulse.Infrastructure `
  --startup-project src/backend/TeamPulse.Api
```

On subsequent runs, the single command `docker compose up -d` starts SQL Server and ensures that the `TeamPulse` database exists.

```powershell
# Backend: restore, build, and test
dotnet restore TeamPulse.sln
dotnet build TeamPulse.sln --no-restore
dotnet test TeamPulse.sln --no-build

# Run the API at http://localhost:5249
dotnet run --project src/backend/TeamPulse.Api
```

In another terminal:

```powershell
cd src/frontend
npm ci
npm run dev
```

Health endpoints:

- `GET http://localhost:5249/health` or `/health/live`: the API process is running.
- `GET http://localhost:5249/health/ready`: the API can connect to SQL Server.

Verify the frontend production build with `npm run build`. Stop local infrastructure with `docker compose down`; the named volume preserves data between runs.

## Continuous Integration

The `CI` workflow runs for every pull request targeting `main`, every push to `main`, and manual dispatches. Its three stable checks are:

- `Backend`: restore, formatting verification, Release build, and .NET tests.
- `Frontend`: `npm ci`, lint, formatting verification, component tests, and production build.
- `CI Gate`: succeeds only when both jobs above succeed.

The workflow has read-only source access and does not consume application secrets. Once the repository has collaborators, configure `CI Gate` as a required status check for `main`.

## Current status

TP-021 adds planning APIs for sprint CRUD, task-to-sprint assignment, and the dependency graph. Self-references, cross-project links, and indirect cycles are rejected. Successors receive incomplete, overdue, and deadline-conflict warnings without TeamPulse automatically rescheduling work. Leaders manage planning while Members and Advisors have read-only access.

## Documentation

Open the project knowledge base at [`docs/TeamPulse-Brain`](docs/TeamPulse-Brain/00-Home/Home.md). Canonical domain language lives in [`CONTEXT.md`](CONTEXT.md), while durable architectural decisions live in [`docs/adr`](docs/adr/).

## License

No license has been selected yet. All rights remain reserved until a license file is added.
