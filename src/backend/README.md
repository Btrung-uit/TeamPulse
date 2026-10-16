# Backend boundaries

- `TeamPulse.Api`: HTTP endpoints, authentication entry points, middleware, and composition root.
- `TeamPulse.Application`: use cases, commands/queries, DTOs, validation, and authorization policies.
- `TeamPulse.Domain`: domain entities, value objects, lifecycle rules, and domain events without infrastructure dependencies.
- `TeamPulse.Infrastructure`: EF Core, SQL Server, identity persistence, file import, GitHub clients, and external adapters.

These are boundaries inside one deployable modular monolith, not separate services.

## Dependency direction

```text
Api → Application → Domain
  └→ Infrastructure → Application + Domain
```

`Domain` has no project dependency. `Api` is the composition root and the only executable backend project.

## Commands

Run from the repository root:

```powershell
dotnet restore TeamPulse.sln
dotnet tool restore
dotnet build TeamPulse.sln --no-restore
dotnet test TeamPulse.sln --no-build
dotnet tool run dotnet-ef database update `
  --project src/backend/TeamPulse.Infrastructure `
  --startup-project src/backend/TeamPulse.Api
dotnet run --project src/backend/TeamPulse.Api
```

The development API listens at `http://localhost:5249`.

## Local configuration

The API requires the `ConnectionStrings:TeamPulse` setting and fails fast when it is missing. Keep the local value outside Git with .NET User Secrets:

```powershell
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "ConnectionStrings:TeamPulse" `
  "Server=localhost,1433;Database=TeamPulse;User Id=sa;Password=<your-password>;TrustServerCertificate=True"
```

Use `/health` or `/health/live` for liveness and `/health/ready` for SQL Server readiness. `/api` returns basic API metadata.

`TeamPulseDbContext` contains the `User`, `Workspace`, `WorkspaceMember`, `Project`, and `Sprint` foundation schema. Its migrations live under `TeamPulse.Infrastructure/Persistence/Migrations`.

Workspace-owned entity queries are denied until a scoped `WorkspaceScope` is selected. The normal HTTP resolver will be added with authentication and membership in TP-011/TP-012. Any future query that deliberately bypasses the filters must explicitly use `IgnoreQueryFilters()` and perform an equivalent authorization check.
