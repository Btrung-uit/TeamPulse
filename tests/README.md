# Test suites

- `backend/TeamPulse.UnitTests`: domain rules, state transitions, permissions, and import validation.
- `backend/TeamPulse.IntegrationTests`: API, EF Core, SQL Server, authorization, and transactional import behavior.
- `frontend`: component and feature integration tests.
- `e2e`: critical user journeys across the deployed application.

Milestone 1 tenant regression coverage lives in `backend/TeamPulse.IntegrationTests/TenantIdentityBoundaryTests.cs`. It must keep two-workspace isolation, database-backed role changes, inactive membership, and `401` versus `403` behavior explicit as new workspace-owned resources are added.
