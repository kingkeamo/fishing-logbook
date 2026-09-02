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

## Identifier naming (mandatory)

- Application data collection tables are **lowercase, plural, unquoted, and contain no
  underscores** (for example `users`, `profiles`, `catches`, `catchphotographs`, `trips`,
  `tripparticipants`, and `userfishingspeciespreferences`).
- Purpose-specific singleton tables may use an explicitly documented singular name.
  `systemhealth` is the current intentional singleton exception. Do not invent another
  singular exception without an explicit reason, and do not revive the legacy
  `SystemTest` name.
- Column names are **lowercase, unquoted, and contain no underscores** (for example
  `id`, `createdon`, and `caughtbyuserid`).
- Primary keys, foreign keys, unique/check constraints, indexes, and explicitly named
  sequences use consistent lowercase names that do not require quoting.
- Do not introduce quoted PascalCase or mixed-case database identifiers. Both application
  SQL and manual SQL must work without quoting database identifiers.
- C# types and properties remain PascalCase. Dapper SQL aliases or explicit persistence
  mappings bridge database names to C# names where Dapper cannot bind them directly.

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
  Outside an explicitly approved pre-release rebaseline, **do not rename, replace, or
  delete** a script that DbUp has journaled; it would be treated as a different migration.
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

### Manual operational scripts

Scripts that an operator must review and run explicitly belong outside the four embedded
folders, under `FishingLogBook.Db.Migrations/Manual/{GitHubIssue}/`. This includes
environment migration, validation, and destructive cleanup scripts. The project file must
not embed `Manual/**/*.sql`, and the normal DbUp runner must never execute them.

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
ALTER TABLE catches ADD COLUMN IF NOT EXISTS releasedon timestamptz;

-- ❌ Same release as the feature
ALTER TABLE catches DROP COLUMN retained;
ALTER TABLE catches RENAME COLUMN retained TO wasretained;
```

Backfills belong in `04_Scripts/` and must be idempotent (`WHERE` the new column is still
null). They still must not drop the source column.

An explicitly approved pre-V1 rebaseline may replace the active migration history so a
fresh database is created directly in the current shape. That exception does not make an
in-place destructive deployment safe: existing databases require separately reviewed
manual scripts, a maintenance window and backup, non-destructive copy and validation while
old/new objects coexist, application proof against the new schema, and only then manual
cleanup. After the rebaseline, normal expand/contract rules apply again.

## Repository pattern (Dapper)

- **Interface:** `FishingLogBook.Application/Contracts/Repositories/I{Entity}Repository.cs`
- **Implementation:** `FishingLogBook.Infrastructure/Persistence/Repositories/{Entity}Repository.cs`
- Inject `IDbConnectionFactory`; open a connection with
  `await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)` inside an
  `await using`.
- Use Dapper with `CommandDefinition` carrying the `CancellationToken`.
- **Parameterised SQL only** — use PascalCase Dapper parameters that align naturally
  with C# names, such as `@CaughtByUserId` and `new { CaughtByUserId = userId }`. The
  lowercase database identifier convention does not apply to parameter names. Never
  interpolate or concatenate user/runtime values into SQL.
- Return **FluentResults** `Result`, `Result<T>` — not exceptions for expected failures
  (not found, constraint, connectivity wrapped as `Fail`). Do not leak Npgsql/Dapper
  types across the boundary. When a `catch` converts an exception into `Result.Fail`,
  log the original exception with `ILogger` first — see **`exception-logging.md`**.
- SQL transactions (begin/commit/rollback) and unique-constraint recovery live here,
  not in CQRS handlers. Handlers orchestrate; they do not hold `NpgsqlTransaction`.
- Filter/query/lookup methods accept `*Args` types from `Application/Args/`.
- Do not create a partially initialised Domain entity merely to transport
  lookup/filter criteria.
- Where a repository hand-copies a persistence row onto a Domain type (or a Domain type onto a
  persistence/parameter type) beyond what Dapper's own column-name binding already does, inject
  `IMapper` and map through it — see **`cqrs.md` → Mapster**, which applies solution-wide.
  Repositories where Dapper binds the query result directly onto the Domain type (no separate row
  class) need no mapper. Domain construction that enforces invariants (for example
  `CatchLocation.TryCreate`) stays explicit, referenced from an `IRegister` conversion rather than
  hand-copied at the call site.
- A repository helper method that builds a substantial or reused set of Dapper SQL parameters
  from a Domain object must not return `object` backed by an anonymous type — the compile-time
  contract is lost once it leaves the method. Return a named internal `*PersistenceParameters`
  type instead (nested in the repository, alongside any row-DTO it already has, e.g.
  `CatchRepository.CatchPersistenceParameters`). This is persistence-boundary shaping (flattening
  a nested Domain value object, normalising timestamps to UTC, casting enums), not object
  adaptation, so build it explicitly rather than through Mapster. A small anonymous object created
  directly at a single inline Dapper call site (not returned from a helper) is still fine.

### Application SQL style and Dapper mapping

- Prefer C# raw string literals (`"""`) for new or modified multiline SQL. Verbatim
  strings (`@"..."`) remain valid, and existing SQL does not need mechanical conversion
  solely for style.
- Dapper's default mapping in this repository is case-insensitive, so a simple lowercase
  column such as `createdon` can bind directly to `CreatedOn`; there is no project-wide
  custom type map. Do not add redundant aliases solely for casing in simple direct
  projections.
- Use explicit property-oriented aliases when a projection is complex, computed, joined,
  mapped to a dedicated row type, or when an alias makes the mapping contract materially
  clearer. Keep aliases unquoted; PostgreSQL folds them to lowercase and Dapper performs
  the case-insensitive match to the PascalCase C# property. An alias does not rename the
  underlying PostgreSQL identifier.

```csharp
const string sql = """
    select
        c.id as Id,
        c.caughtbyuserid as CaughtByUserId,
        c.recordedbyuserid as RecordedByUserId,
        c.createdon as CreatedOn
    from catches c
    where c.caughtbyuserid = @CaughtByUserId;
    """;

var catches = await connection.QueryAsync<Catch>(
    new CommandDefinition(
        sql,
        new { CaughtByUserId = userId },
        cancellationToken: cancellationToken));
```

Manual SQL uses literal values appropriate to the SQL client, while retaining unquoted
database identifiers:

```sql
select *
from catches
where caughtbyuserid = '00000000-0000-0000-0000-000000000000';
```

Never construct application SQL from runtime values:

```csharp
// Good: the runtime value is a Dapper parameter.
const string sql = """
    select *
    from catches
    where caughtbyuserid = @CaughtByUserId;
    """;

// Bad: runtime values must not be concatenated or interpolated into SQL.
var unsafeSql = "select * from catches where caughtbyuserid = '" + userId + "';";
```

GOOD:

```csharp
CreateAsync(User user, UserIdentity identity, cancellationToken);
FindUserIdAsync(FindUserIdentityArgs args, cancellationToken);
```

BAD:

```csharp
CreateAsync(Guid userId, Guid identityId, string provider, string subject, string email, ...);
FindUserIdAsync(new UserIdentity { Provider = provider, Subject = subject }, ...);
```

Repository create/update operations accept meaningful Domain concepts. Infrastructure
may decompose those objects into SQL parameters internally.

`User` is the FishingLogBook business entity. `ICurrentUser` is the request-scoped
Application indication of which User is authenticated. Repositories persist `User` /
`UserIdentity`; they do not take `ICurrentUser`.

## Connection & configuration

- Connection string: `ConnectionStrings:Postgres` (env var `ConnectionStrings__Postgres`).
  Supplied via user secrets locally and environment variables in Dev/Prod.
- Never commit live database credentials. Never log connection strings.
- `IDbConnectionFactory` is registered in Infrastructure DI. If no connection string is
  configured, the factory throws a clear error rather than returning a fake connection.

## Live-database repository tests

API tests mock repositories and must not require live PostgreSQL.

When uniqueness, transactions, or concurrency must be proven against a real database,
put those tests in `tests/FishingLogBook.Infrastructure.Tests/Repositories/`:

```text
FishingLogBook.Infrastructure.Tests/
    {Sut}Tests/                         → unit tests (no live database)
    Repositories/                       → live-database tests (Testcontainers)
        TestSupport/
            PostgresFixture.cs
        Repositories/
            {Repository}Tests/
        Migrations/
            SchemaTests/
        NpgsqlConnectionFactoryTests/
```

`Repositories/` is the live-database category (parallel to `Logging/`, `Storage/`, and
other config-test groupings at the project root); `Repositories/Repositories/` is the
subfolder specifically for `*Repository` test suites, alongside sibling live-DB areas
(`Migrations/SchemaTests/`, `NpgsqlConnectionFactoryTests/`) that also need Postgres but
are not themselves repositories. Example:
`Repositories/Repositories/UserIdentityRepositoryTests/`. Use the word **Repositories**,
not Integration or Sandbox.

These tests run in normal GitHub Actions CI via Testcontainers PostgreSQL on the
hosted Ubuntu runner. They do **not** use Neon, a shared CI database, or database
connection secrets. Do not add a workflow Postgres service unless a later issue
actually requires one.

Do not place live-database repository tests next to unit tests at the project root.
Folder and naming conventions are in **`testing-csharp.md`**.

## Before writing

- **New migration:** add a `YYYYMMDDHHMM_{GitHubIssue}_{Description}.sql` file under the
  appropriate numbered folder in `FishingLogBook.Db.Migrations` (read the existing scripts
  for style); the embedding globs pick it up automatically. Additive (expand) only in a
  feature deploy — see **Expand / contract** above. If the change will later drop a column,
  open the cleanup task before merging. Do not rename already-applied scripts.
- **New repository:** read neighbouring repositories and the relevant repository contract
  before implementing.
