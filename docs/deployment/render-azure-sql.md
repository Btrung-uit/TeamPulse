# Render with Azure SQL

This deployment runs the React SPA and ASP.NET Core API in one Render Web Service. Azure SQL remains an external managed database.

```text
browser -> Render Web Service -> Azure SQL Database
```

The root `Dockerfile` builds the frontend, publishes the API, creates an EF Core migration bundle, and packages them in one runtime image. The container applies pending migrations before starting the API. This start-time migration strategy is intended for the single-instance free demo; use Render's paid pre-deploy command before scaling beyond one instance.

## Required Render settings

The existing manually created service can use these settings:

- Runtime: Docker.
- Dockerfile path: `./Dockerfile`.
- Docker context: `.`.
- Region: Singapore.
- Health check path: `/health/ready`.
- Plan: Free for a portfolio/demo deployment.

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

The first start can fail at the migration step until these rules are present. The entrypoint retries transient migration failures without printing the connection string.

## Verification

After a successful deployment:

```text
https://<service>.onrender.com/health/live
https://<service>.onrender.com/health/ready
```

Both endpoints must return a successful response. Then verify registration, login, workspace creation, and a database-backed task flow through the public URL.
