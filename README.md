# FishingLogBook

FishingLogBook is a mobile-first, offline-first fishing platform for anglers, fishing
guides, fisheries and competition organisers. See [`docs/Requirements.md`](docs/Requirements.md)
for the product requirements and [`docs/Architecture.md`](docs/Architecture.md) for the
architecture and hosting decisions.

This repository currently contains the **initial technical foundation** described in
[`BUILD.md`](BUILD.md). The goal of this stage is to prove the vertical architecture
(PWA → API → PostgreSQL) cheaply and safely — not to build the full MVP. Fisheries,
guides, competitions, bookings, authentication (Cognito) and photo storage (R2) are
intentionally **not** implemented yet.

## Solution structure

```text
src/
  FishingLogBook.Domain          # Entities, no project dependencies
  FishingLogBook.Shared          # API contracts shared with the Web client
  FishingLogBook.Application     # Application services + contracts (-> Domain, Shared)
  FishingLogBook.Infrastructure  # Dapper repositories (-> Application, Domain)
  FishingLogBook.DependencyInjection # Composition root wiring every layer (-> Application, Infrastructure)
  FishingLogBook.Db.Migrations       # DbUp SQL scripts (embedded) + migration engine
  FishingLogBook.Db.Migrations.App   # Console migration runner (local + pipeline)
  FishingLogBook.Api             # ASP.NET Core minimal API (-> Application, DependencyInjection, Shared)
  FishingLogBook.Web             # Blazor WebAssembly PWA + MudBlazor (-> Shared only)
tests/                           # One test project per production project (+ shared helpers)
  FishingLogBook.Tests.Common        # Shared builders/fixtures (no tests)
  FishingLogBook.Shared.Tests
  FishingLogBook.Application.Tests
  FishingLogBook.Infrastructure.Tests
  FishingLogBook.Db.Migrations.Tests
  FishingLogBook.Api.Tests
  FishingLogBook.Web.Tests           # bUnit component tests
infrastructure/terraform/        # Terraform skeleton (manual apply only — see infrastructure/README.md)
.github/workflows/               # CI: build, test, Terraform validation
```

The Blazor WebAssembly client must never reference `Application` or `Infrastructure`; it
communicates with the API over HTTP and depends only on `Shared`.

## Technology

| Concern            | Choice                                  |
|--------------------|-----------------------------------------|
| Target framework   | .NET 10                                 |
| API                | ASP.NET Core minimal APIs               |
| Data access        | Dapper + Npgsql (PostgreSQL)            |
| Migrations         | DbUp (embedded SQL scripts) + console runner |
| Web                | Blazor WebAssembly PWA + MudBlazor      |
| Tests              | xUnit, NSubstitute, AwesomeAssertions, bUnit |
| Infrastructure     | Terraform (manual apply only)           |

Entity Framework is intentionally **not** used. Database table names must not contain
underscores.

NuGet package versions are managed centrally in [`Directory.Packages.props`](Directory.Packages.props)
(Central Package Management). Reference packages in `.csproj` files without a `Version`
attribute, and add or bump versions in that single file.

## Prerequisites

- .NET 10 SDK
- A local PostgreSQL 13+ instance (for the database endpoint and migrations)

## Configuration

No credentials are committed. The API reads its PostgreSQL connection string from the
`ConnectionStrings:Postgres` configuration value (or the `ConnectionStrings__Postgres`
environment variable). For local development, use user secrets:

```bash
cd src/FishingLogBook.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=fishinglogbook;Username=<user>;Password=<password>"
```

Database migrations are **not** run by the API. They live in `FishingLogBook.Db.Migrations`
and are applied by the `FishingLogBook.Db.Migrations.App` console runner (see
[Database migrations](#database-migrations) below).

The Web client reads the API base URL from `wwwroot/appsettings.json`
(`Api:BaseUrl`). The `Development` override targets the local API at
`https://localhost:7256`. API URLs are configuration, never hard-coded in source.

## Database migrations

SQL scripts and the DbUp engine live in `FishingLogBook.Db.Migrations`; they are applied by
the `FishingLogBook.Db.Migrations.App` console runner (the API never migrates on startup).

Scripts live under numbered folders (`01_Tables`, `02_SeedData`, `03_Routines`,
`04_Scripts`) and are named `YYYYMMDDHHMM_Description.sql`. They are **ordered by filename
only** (via `FilenameOnlyScriptComparer`), so the timestamp prefix determines run order
across all folders — a script authored earlier always runs first. DbUp records applied
scripts in its `SchemaVersions` table and runs each once.

The runner reads `Db:ConnectionString` (user secrets, `Db__ConnectionString` env var, or a
local `appsettings.Development.json`):

```bash
cd src/FishingLogBook.Db.Migrations.App
dotnet user-secrets set "Db:ConnectionString" "Host=localhost;Port=5432;Database=fishinglogbook;Username=<user>;Password=<password>"

# Interactive: preview the pending scripts, then choose to run
dotnet run

# Non-interactive (CI/pipeline): apply immediately (exit code 0 on success)
dotnet run -- --run
```

## Local vertical slice

Run the API and the Web PWA in two terminals:

```bash
# Terminal 1 - API (https://localhost:7256)
dotnet run --project src/FishingLogBook.Api

# Terminal 2 - Web PWA (https://localhost:7005)
dotnet run --project src/FishingLogBook.Web
```

Open the Web app. The system status page should show:

```text
Web:      Online
API:      Online
Database: Online   (once PostgreSQL is configured and migrated)
```

Use the translate icon in the app bar to switch between English and French. The choice is
stored in the browser. The database seed name is not translated (it comes from PostgreSQL).

The API exposes:

- `GET /health` — liveness, returns `{ "status": "Healthy" }`
- `GET /api/system/database` — performs a real query against the `SystemTest` table and
  returns the seeded record. Returns HTTP 503 if the database cannot be reached (no faked
  health).

In Development, starting the API over HTTPS also opens Swagger UI at
`https://localhost:7256/swagger` (OpenAPI document at `/openapi/v1.json`). Swagger UI is
not enabled outside Development.

## Build and test

```bash
dotnet build FishingLogBook.sln
dotnet test FishingLogBook.sln
```

## Container

The API has a provider-neutral multi-stage [`Dockerfile`](Dockerfile) that listens on
port `8080`, runs as a non-root user, and takes all configuration from environment
variables. No secrets are embedded in the image.

## Infrastructure

Terraform is **manual only**. Never run `terraform apply`/`destroy` from CI. See
[`infrastructure/README.md`](infrastructure/README.md) for the layout, the cost/safety
warning, and the manual deployment process.
