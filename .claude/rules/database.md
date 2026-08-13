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

- Filename convention: **`YYYYMMDDHHMM_Description.sql`** (timestamp prefix), e.g.
  `202608131200_CreateSystemTest.sql`.
- **Ordering is by filename only, not folder** — `FilenameOnlyScriptComparer`
  (`WithScriptNameComparer`) strips the assembly/folder prefix and sorts on the timestamp
  filename, so a script authored earlier always runs before a later one regardless of which
  numbered folder it sits in. The folder is only a tie-breaker when timestamps are equal.
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
Dapper layer (seed/DDL scripts are static SQL).

## Repository pattern (Dapper)

- **Interface:** `FishingLogBook.Application/Contracts/I{Entity}Repository.cs`
- **Implementation:** `FishingLogBook.Infrastructure/Persistence/{Entity}Repository.cs`
- Inject `IDbConnectionFactory`; open a connection with
  `await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)` inside an
  `await using`.
- Use Dapper with `CommandDefinition` carrying the `CancellationToken`.
- **Parameterised SQL only** — use Dapper `@ParamName` parameters, never string
  concatenation for values.
- Return domain entities (or `null`) — do not leak Npgsql/Dapper types across the boundary.

## Connection & configuration

- Connection string: `ConnectionStrings:Postgres` (env var `ConnectionStrings__Postgres`).
  Supplied via user secrets locally and environment variables in Dev/Prod.
- Never commit live database credentials. Never log connection strings.
- `IDbConnectionFactory` is registered in Infrastructure DI. If no connection string is
  configured, the factory throws a clear error rather than returning a fake connection.

## Before writing

- **New migration:** add a `YYYYMMDDHHMM_Description.sql` file under the appropriate
  numbered folder in `FishingLogBook.Db.Migrations` (read the existing scripts for style);
  the embedding globs pick it up automatically.
- **New repository:** read `SystemRepository` and `ISystemRepository` before implementing.
