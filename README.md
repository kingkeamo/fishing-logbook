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
  FishingLogBook.Infrastructure  # Dapper repositories, DbUp migrator (-> Application, Domain)
  FishingLogBook.Api             # ASP.NET Core minimal API (-> Application, Infrastructure, Shared)
  FishingLogBook.Web             # Blazor WebAssembly PWA + MudBlazor (-> Shared only)
tests/
  FishingLogBook.UnitTests
  FishingLogBook.IntegrationTests
  FishingLogBook.WebTests        # bUnit component tests
database/migrations/             # DbUp SQL scripts (no underscores in table names)
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
| Migrations         | DbUp (embedded SQL scripts)             |
| Web                | Blazor WebAssembly PWA + MudBlazor      |
| Tests              | xUnit, NSubstitute, FluentAssertions, bUnit |
| Infrastructure     | Terraform (manual apply only)           |

Entity Framework is intentionally **not** used. Database table names must not contain
underscores.

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

In `Development`, the API runs DbUp migrations on startup (`Database:RunMigrationsOnStartup`
is `true`). In other environments this defaults to `false` so migration execution can be
separated from application startup later.

The Web client reads the API base URL from `wwwroot/appsettings.json`
(`Api:BaseUrl`). The `Development` override targets the local API at
`https://localhost:7256`. API URLs are configuration, never hard-coded in source.

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

The API exposes:

- `GET /health` — liveness, returns `{ "status": "Healthy" }`
- `GET /api/system/database` — performs a real query against the `SystemTest` table and
  returns the seeded record. Returns HTTP 503 if the database cannot be reached (no faked
  health).

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
