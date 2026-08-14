---
paths:
  - "src/FishingLogBook.Db.Migrations/**/*.sql"
  - "src/FishingLogBook.Db.Migrations/**/*.cs"
  - "src/FishingLogBook.Db.Migrations.App/**/*.cs"
  - "src/FishingLogBook.Infrastructure/Persistence/**/*.cs"
  - "src/FishingLogBook.Application/Contracts/**/*.cs"
---

# Database & Persistence Conventions

## Stack

| Concern | Tool |
|---------|------|
| Migrations | **DbUp** (embedded SQL scripts) |
| Runtime access | **Dapper** + **Npgsql** (PostgreSQL) |
| **Not used** | Entity Framework, FluentMigrator, `DbContext` |

## Table naming (mandatory)

- Database table names must **not** contain underscores (e.g. `SystemTest`, not
  `System_Test`).
- Identifiers are created and queried **quoted** with PascalCase (e.g. `"SystemTest"`,
  `"CreatedOn"`) so PostgreSQL preserves the casing. Be consistent between DDL and queries.

## DbUp migrations — two dedicated projects

Migrations are **separate from the API and Infrastructure**, in two projects:

- **`FishingLogBook.Db.Migrations`** — holds the SQL scripts (embedded) plus the DbUp
  wiring (`MigrationService`, `FilenameOnlyScriptComparer`, `PostgresDatabaseHelper`).
- **`FishingLogBook.Db.Migrations.App`** — a console **runner** used to apply migrations
  locally, in a pipeline, or ad hoc. The API never runs migrations on startup.

### Script folders and naming (mandatory)

Scripts live under numbered folders inside `FishingLogBook.Db.Migrations`:

```text
01_Tables/     02_SeedData/     03_Routines/     04_Scripts/
```

- Filename convention: **`YYYYMMDDHHMM_{GitHubIssue}_{Description}.sql`** (timestamp
  prefix, then the GitHub **issue number**, then a PascalCase description). No `#` in
  the filename. Example: `202608141200_3_AddCatchTable.sql` for issue `#3`.
  Scripts already applied (`202608131200_CreateSystemTest.sql`,
  `202608131201_SeedSystemTest.sql`) stay as they are — **do not rename** a script that
  DbUp has journaled; it would re-run as a new migration.
- **Ordering is by filename only, not folder** — `FilenameOnlyScriptComparer`
  (`WithScriptNameComparer`) strips the assembly/folder prefix and sorts alphabetically
  on the filename, so the `YYYYMMDDHHMM` prefix determines run order. The issue number
  sits after the timestamp and does not affect ordering when timestamps differ. The
  folder is only a tie-breaker when the filename portion is equal.
- All `*.sql` under the four folders are embedded automatically by the csproj globs — no
  per-file entry needed. DbUp records applied scripts in its `SchemaVersions` journal and
  runs each once, `WithTransactionPerScript`.
- Prefer idempotent seed scripts (`WHERE NOT EXISTS`, etc.). Migration progress is logged
  via `ILogger` (`MigrationService.DbUpLogger`).

### Running migrations

The runner reads `Db:ConnectionString` (from `appsettings.json`, user secrets, or the
`Db__ConnectionString` env var):

```bash
# Interactive (local): preview scripts, then choose to run
dotnet run --project src/FishingLogBook.Db.Migrations.App

# Non-interactive (CI/pipeline): apply immediately, exit code 0 = success
dotnet run --project src/FishingLogBook.Db.Migrations.App -- --run
```

It auto-runs non-interactively when `--run`/`--yes`/`-y` is passed or stdin is redirected.

**SQL style:** PostgreSQL syntax; one logical change per script; parameterised at the
Dapper layer (seed/DDL scripts are static SQL). Prefer idempotent alters:
`ADD COLUMN IF NOT EXISTS`.

## Expand / contract — no destructive feature deploys (mandatory)

Never ship a migration that can lose data in the same deployment as the feature that
needs the schema change. DbUp runs each script once; there is no rollback.

**Forbidden in a feature's migration(s)** (the scripts that go out with the code that
starts using the new shape):

- `DROP COLUMN`, `DROP TABLE`, `DROP TYPE`, `DROP INDEX` that the running app still needs
- In-place `RENAME` of a column or table
- Tightening constraints against existing rows (`SET NOT NULL` without a completed
  backfill, shrinking length, adding a unique constraint that current data would violate)
- `DELETE` / `TRUNCATE` of production data

**Required sequence:**

1. **Expand** — additive only in this feature's script: `ADD COLUMN` (nullable or with a
   safe default), new tables, new indexes. **Leave the old column/table in place.**
   Application code may dual-write and dual-read until the new path is proven.
2. **Feature live** — production is on the new path and the new column/table is populated.
3. **Contract (cleanup)** — a **separate** follow-up task, not the same PR or release as
   the feature. Only then add a script that drops the old column/table or stops writing
   it. Create that follow-up task **before merging the expand PR** so cleanup is not
   forgotten. Do not drop "later" with no tracked task.

If a rename is required: add the new column → backfill (same expand phase or a dedicated
`04_Scripts` backfill) → switch reads/writes → drop the old column in the cleanup task.

```sql
-- ✅ Expand (ships with the feature)
ALTER TABLE "Catch" ADD COLUMN IF NOT EXISTS "CaughtOn" timestamptz;

-- ❌ Same release as the feature
ALTER TABLE "Catch" DROP COLUMN "CaughtDate";
ALTER TABLE "Catch" RENAME COLUMN "CaughtDate" TO "CaughtOn";
```

Backfills belong in `04_Scripts/` and must be idempotent (`WHERE` the new column is still
null). They still must not drop the source column.

## Repository pattern (Dapper)

- **Interface:** `FishingLogBook.Application/Contracts/Repositories/I{Entity}Repository.cs`
- **Implementation:** `FishingLogBook.Infrastructure/Persistence/{Entity}Repository.cs`
- Inject `IDbConnectionFactory`; open a connection with
  `await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)` inside an
  `await using`.
- Use Dapper with `CommandDefinition` carrying the `CancellationToken`.
- **Parameterised SQL only** — use Dapper `@ParamName` parameters, never string
  concatenation for values.
- Return **FluentResults** `Result`, `Result<T>` — not exceptions for expected failures
  (not found, constraint, connectivity wrapped as `Fail`). Do not leak Npgsql/Dapper
  types across the boundary.
- Filter/query methods accept `*Args` types from `Application/Args/` when they take more
  than one or two parameters.

## Connection & configuration

- Connection string: `ConnectionStrings:Postgres` (env var `ConnectionStrings__Postgres`).
  Supplied via user secrets locally and environment variables in Dev/Prod.
- Never commit live database credentials. Never log connection strings.
- `IDbConnectionFactory` is registered in Infrastructure DI. If no connection string is
  configured, the factory throws a clear error rather than returning a fake connection.

## Before writing

- **New migration:** add a `YYYYMMDDHHMM_{GitHubIssue}_{Description}.sql` file under the
  appropriate numbered folder in `FishingLogBook.Db.Migrations` (read the existing scripts
  for style); the embedding globs pick it up automatically. Additive (expand) only in a
  feature deploy — see **Expand / contract** above. If the change will later drop a column,
  open the cleanup task before merging. Do not rename already-applied scripts.
- **New repository:** read `SystemRepository` and `ISystemRepository` before implementing.
