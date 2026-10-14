# Backend boundaries

- `TeamPulse.Api`: HTTP endpoints, authentication entry points, middleware, and composition root.
- `TeamPulse.Application`: use cases, commands/queries, DTOs, validation, and authorization policies.
- `TeamPulse.Domain`: domain entities, value objects, lifecycle rules, and domain events without infrastructure dependencies.
- `TeamPulse.Infrastructure`: EF Core, SQL Server, identity persistence, file import, GitHub clients, and external adapters.

These are boundaries inside one deployable modular monolith, not separate services.
