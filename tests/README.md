# Test suites

- `backend/TeamPulse.UnitTests`: domain rules, state transitions, permissions, and import validation.
- `backend/TeamPulse.IntegrationTests`: API, EF Core, SQL Server, authorization, and transactional import behavior.
- `frontend`: component and feature integration tests.
- `e2e`: critical user journeys across the deployed application.

Milestone 1 tenant regression coverage lives in `backend/TeamPulse.IntegrationTests/TenantIdentityBoundaryTests.cs`. It must keep two-workspace isolation, database-backed role changes, inactive membership, and `401` versus `403` behavior explicit as new workspace-owned resources are added.

## Milestone 2 work-management baseline

The TP-026 regression baseline is intentionally split by test level:

| Risk | Test suite |
| --- | --- |
| Invalid lifecycle transitions | `backend/TeamPulse.UnitTests/TaskLifecycleTests.cs` |
| Dependency cycles and risk evaluation | `backend/TeamPulse.UnitTests/TaskPlanningDomainTests.cs` |
| Permissions, archive history, cross-workspace access, and stale `RowVersion` conflicts | `backend/TeamPulse.IntegrationTests/TaskEndpointsTests.cs` |
| Cross-project dependencies and planning permissions | `backend/TeamPulse.IntegrationTests/PlanningEndpointsTests.cs` |
| Task form validation and API submission boundaries | `src/frontend/src/features/tasks/TaskBoardFlow.test.tsx` |
| Status badges and task-card state cues | `src/frontend/src/components/ui/ComponentPrimitives.test.tsx` |

Run the same complete baseline used by CI from the repository root:

```bash
dotnet test TeamPulse.sln --configuration Release
npm --prefix src/frontend run test
```
