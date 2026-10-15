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

The development API listens at `http://localhost:5249`. Its foundation endpoints are `/health` and `/api`.
