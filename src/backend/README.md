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
dotnet build TeamPulse.sln --no-restore
dotnet test TeamPulse.sln --no-build
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

`TeamPulseDbContext` intentionally has no domain schema yet. Entities and the first migration belong to TP-010.
