---
paths:
  - "database/migrations/**/*.sql"
  - "src/FishingLogBook.Infrastructure/Persistence/**/*.cs"
  - "src/FishingLogBook.Infrastructure/Migrations/**/*.cs"
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

## DbUp migrations (`database/migrations/`)

- SQL scripts live under `database/migrations/` at the repository root.
- Naming: numeric prefix + description, e.g. `001_CreateSystemTest.sql`,
  `002_SeedSystemTest.sql`. DbUp runs scripts in ascending name order, once each.
- Scripts are embedded into `FishingLogBook.Infrastructure` via the csproj link so DbUp
  works reliably inside the container:

```xml
<ItemGroup>
  <EmbeddedResource Include="..\..\database\migrations\*.sql" LinkBase="Migrations" />
</ItemGroup>
```

  This yields resource names like `FishingLogBook.Infrastructure.Migrations.001_CreateSystemTest.sql`.
  A new `.sql` file placed in `database/migrations/` is picked up automatically by the
  glob — no per-file csproj entry required. If a migration does not run, confirm the file
  is in that folder and the build embedded it (see `MigrationScriptEmbeddingTests`).
- Migration execution is explicit and logged (`DbUpDatabaseMigrator` logs via `ILogger`
  through `LoggerUpgradeLog`). Prefer idempotent seed scripts (`WHERE NOT EXISTS`, etc.).
- The API may run migrations on startup in `Development`
  (`Database:RunMigrationsOnStartup`). Production keeps this configurable so migration
  execution can later be separated from application startup.

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

- **New migration:** read the existing scripts in `database/migrations/` for naming and
  style; confirm the embedding glob covers your file.
- **New repository:** read `SystemRepository` and `ISystemRepository` before implementing.
