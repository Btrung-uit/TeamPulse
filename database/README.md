# Database assets

EF Core migrations live under `src/backend/TeamPulse.Infrastructure/Persistence/Migrations`. This folder is reserved for reviewed seed datasets, import templates, and database assets that are useful outside the compiled application.

`init/001-create-database.sql` is an idempotent local bootstrap script used by Docker Compose. It creates the empty `TeamPulse` database only when it does not already exist; it does not own the future domain schema or EF Core migrations.
