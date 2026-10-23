# Render with Azure SQL

This deployment runs the React SPA and ASP.NET Core API in one Render Web Service. Azure SQL remains an external managed database.

```text
browser -> Render Web Service -> Azure SQL Database
```

The root `Dockerfile` builds the frontend, publishes the API, creates an EF Core migration bundle, and packages them in one runtime image. The container applies pending migrations before starting the API. This start-time migration strategy is intended for the single-instance free demo; use Render's paid pre-deploy command before scaling beyond one instance.

## Current portfolio environment

- Application: [https://teampulse-xgq8.onrender.com](https://teampulse-xgq8.onrender.com)
- Render region: Singapore.
- Database provider: Azure SQL Database.
- Deployment source: `main`.
- Recommended auto-deploy policy: deploy only after GitHub CI checks pass (`autoDeployTrigger: checksPass`).

The generated `onrender.com` hostname can change when the Web Service is deleted and recreated. If it changes, update the GitHub repository homepage and every live-demo link in `README.md` and this runbook.

## Required Render settings

The existing manually created service can use these settings:

- Runtime: Docker.
- Dockerfile path: `./Dockerfile`.
- Docker context: `.`.
- Region: Singapore.
- Health check path: `/health/ready`.
- Plan: Free for a portfolio/demo deployment.
- Auto-deploy: After CI checks pass.

Set these environment variables in Render. Never commit their real values:

| Key                               | Value                                            |
| --------------------------------- | ------------------------------------------------ |
| `ASPNETCORE_ENVIRONMENT`          | `Production`                                     |
| `RUN_MIGRATIONS`                  | `true`                                           |
| `ConnectionStrings__TeamPulse`    | Azure SQL ADO.NET connection string              |
| `Authentication__Jwt__SigningKey` | Random secret containing at least 32 UTF-8 bytes |

The Azure SQL connection string must use encryption and certificate validation:

```text
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

The committed `render.yaml` describes the same service for a new Blueprint. `ConnectionStrings__TeamPulse` is prompted as a secret, and Render generates the JWT signing key.

## Azure SQL firewall

Render traffic does not become trusted by enabling Azure's "Allow Azure services and resources to access this server" option. Keep that option disabled.

After the Render service exists:

1. Open the service's **Connect > Outbound** tab.
2. Copy every outbound CIDR range for the service.
3. Add corresponding start/end IPv4 firewall rules to the Azure SQL logical server.
4. Redeploy the latest commit.

Do not copy outbound ranges from an older service or a different region. Recheck **Connect > Outbound** whenever the service is recreated or moved, add every current range, and remove obsolete firewall rules only after the replacement deployment passes readiness.

The first start can fail at the migration step until these rules are present. The entrypoint retries transient migration failures without printing the connection string. Azure firewall changes can also take a few minutes to propagate.

## Verification

Current endpoints:

```text
https://teampulse-xgq8.onrender.com/health/live
https://teampulse-xgq8.onrender.com/health/ready
```

Both endpoints must return HTTP `200`; readiness must fail when Azure SQL is unavailable. Then verify registration, login, workspace creation, and a database-backed task flow through the public URL.

For a newly created service, verify this checklist before declaring the deployment complete:

1. All four environment variables are present and secrets are not visible in logs or source control.
2. Azure SQL allows every current Render outbound range.
3. `/health/live` and `/health/ready` return HTTP `200`.
4. Registration and login persist data in Azure SQL.
5. The GitHub repository homepage points to the current public URL.
