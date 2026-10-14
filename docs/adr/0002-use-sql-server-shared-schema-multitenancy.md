---
status: accepted
---

# Use SQL Server with workspace-scoped shared-schema multitenancy

TeamPulse will store application data in SQL Server through Entity Framework Core, with workspace ownership enforced through membership and workspace-scoped relationships in a shared schema. Separate databases per workspace were rejected because they add provisioning and migration overhead that is unnecessary for the MVP, while the shared model still requires explicit authorization and indexing to prevent cross-workspace access.
