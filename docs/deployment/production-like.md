# Production-like deployment

TP-052 packages TeamPulse as four containers:

```text
browser -> frontend/nginx :8080 -> api :8080 -> SQL Server
                                  ^
                         one-shot EF migrator
```

The API is not published directly. Nginx serves the React SPA, proxies `/api` and `/health`, and applies baseline response headers. The migrator must finish successfully before the API starts.

## Run the complete stack locally

Requirements: Docker Engine with Compose v2 and at least 4 GB available to Docker Desktop.

```powershell
# Generates .env once with random local secrets and refuses to overwrite it.
.\scripts\New-DeploymentEnv.ps1

docker compose --profile app up --build -d
docker compose --profile app ps -a
```

Expected state:

- `sqlserver`, `api`, and `frontend` are healthy/running.
- `migrate` exits with code `0` after applying pending EF Core migrations.
- `http://localhost:8080` opens TeamPulse.
- `http://localhost:8080/health/live` returns `Healthy`.
- `http://localhost:8080/health/ready` returns `Healthy` only when SQL Server is reachable.

If Windows reserves port `8080`, change `APP_PORT` in `.env` (for example to `8088`) and use that port in the URLs below.

The environment script refuses to generate a new SQL password when it detects an existing TeamPulse database volume. If `.env` was lost, recover the previous password or deliberately rotate the `sa` login first; deleting the volume is not a password-reset procedure and would destroy local data.

Check the logs without printing environment variables:

```powershell
docker compose --profile app logs migrate
docker compose --profile app logs --tail 100 api frontend
```

To test on a phone connected to the same trusted network, allow TCP port `8080` in the local firewall temporarily and open `http://<computer-ipv4>:8080`. Remove that firewall rule after testing. A public demo must use HTTPS, not a directly exposed development port.

## Configuration and secrets

Compose reads `.env`, which is ignored by Git. Required values are:

- `MSSQL_SA_PASSWORD`: SQL Server password; avoid semicolons because it is embedded in a connection string.
- `JWT_SIGNING_KEY`: random value of at least 32 UTF-8 bytes.
- `APP_HOST` and `APP_PORT`: public bind address and port for the frontend proxy.

Do not put real secrets in `.env.example`, Compose YAML, Dockerfiles, source-controlled appsettings, image build arguments, or CI logs. In a hosted environment, inject the connection string and JWT key from that platform's secret store.

## Safe migration behavior

The `migration` image contains an EF Core migration bundle. Deployment order is:

1. Wait for SQL Server health.
2. Run the bundle once; EF records applied migrations in `__EFMigrationsHistory`.
3. Start the API only after the migrator exits successfully.
4. Start the frontend only after API readiness succeeds.

The API never runs migrations during application startup. Two releases must not run their migrator against the same database concurrently.

## Deploy from `main`

For a single-host demo environment:

1. Provision a Linux host with Docker Compose, persistent storage, DNS, and an HTTPS reverse proxy.
2. Clone the repository and create `.env` directly on the host with production secrets. Set `APP_HOST=127.0.0.1` so only the reverse proxy can reach port 8080.
3. Point the HTTPS proxy to `http://127.0.0.1:8080` and preserve `X-Forwarded-Proto: https`.
4. On each release, fetch `main`, back up the database, build images, run the migrator, then recreate the app:

```bash
git fetch origin main
git checkout main
git pull --ff-only origin main
docker compose --profile app build
docker compose --profile app up -d
docker compose --profile app ps -a
curl --fail --show-error --silent https://your-domain.example/health/ready
```

The CI Docker job builds the same three application targets before changes can pass the repository quality gate. Connecting automatic delivery to a real host requires provider credentials and is intentionally not represented by committed placeholder secrets.

## Backup and rollback

Before a release that contains migrations, create and copy a SQL Server backup to storage outside the container volume. Never treat `docker compose down` as a database backup.

Application rollback:

1. Check out the last known-good commit or immutable image tag.
2. Run `docker compose --profile app build` and `docker compose --profile app up -d`.
3. Verify `/health/ready`, login, and the core demo flow.

Database rollback:

- Prefer a forward-fix migration when the schema remains compatible.
- If the migration is destructive or incompatible, stop `api`, restore the pre-release backup, then deploy the matching application commit.
- Do not run `dotnet ef database update <old-migration>` automatically in production; down migrations can destroy data.

## Stop and clean up

```powershell
# Keeps the named SQL Server volume.
docker compose --profile app down

# Destructive: removes the database volume. Use only for disposable local data.
docker compose --profile app down --volumes
```
