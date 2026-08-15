---
paths:
  - "src/FishingLogBook.Api/**/*.cs"
  - "src/FishingLogBook.Application/**/*.cs"
  - "src/FishingLogBook.Domain/**/*.cs"
  - "src/FishingLogBook.Infrastructure/**/*.cs"
  - "src/FishingLogBook.Shared/**/*.cs"
---

# C# Coding Conventions

## Solution structure

| Project | Role |
|---------|------|
| `FishingLogBook.Domain` | Entities and domain types. **No project dependencies.** |
| `FishingLogBook.Shared` | Shared `*Dto` / `*Enum` / `*Constants` between API and Web. |
| `FishingLogBook.Application` | MediatR CQRS, FluentValidation, `ValidatedResponse` — see **`cqrs.md`**. |
| `FishingLogBook.Infrastructure` | Dapper repositories, Npgsql connection factory. |
| `FishingLogBook.DependencyInjection` | Composition root (`AddFishingLogBook`) — see below. |
| `FishingLogBook.Db.Migrations` | DbUp SQL scripts + migration engine — see **`database.md`**. |
| `FishingLogBook.Db.Migrations.App` | Console migration runner — see **`database.md`**. |
| `FishingLogBook.Api` | ASP.NET Core minimal API host; thin endpoint mappings. |
| `FishingLogBook.Web` | Blazor WebAssembly PWA — see **`blazor.md`**. |

**Dependency direction (do not violate):**

```text
Domain               -> (nothing)
Shared               -> (nothing)
Application          -> Domain, Shared
Infrastructure       -> Application, Domain
DependencyInjection  -> Application, Infrastructure
Db.Migrations        -> (nothing; standalone, embeds SQL)
Db.Migrations.App    -> Db.Migrations
Api                  -> Application, DependencyInjection, Shared
Web                  -> Shared only
```

The Blazor WebAssembly project must **never** reference `Application` or `Infrastructure`.
Do not expose server-side implementation assemblies to the WebAssembly client.

**Do not introduce:** Entity Framework / `DbContext`, FluentMigrator, Wolverine, MediatR
13+ / Lucky Penny, or a second mediator. Persistence is Dapper + DbUp. Application work
is **CQRS via MediatR 12.5.0 (Apache-2.0) + FluentValidation + Mapster + FluentResults**
— see **`cqrs.md`**. Add further abstractions only when they provide genuine value.

## Database and migrations

Data access and migration rules live in **`database.md`**. Summary: PostgreSQL via
**Dapper + Npgsql**; migrations via **DbUp** in the dedicated `FishingLogBook.Db.Migrations`
project (embedded SQL scripts) applied by the `FishingLogBook.Db.Migrations.App` runner —
the API does not migrate on startup. Table names must **not** contain underscores.
Feature migrations are **expand-only** (add column/table, keep the old one). Destructive
cleanup (`DROP COLUMN`, etc.) is a separate follow-up task after the feature is live —
see **`database.md`**.

## General C# style

- Target: **.NET 10**, `Nullable enable`, `ImplicitUsings enable` (consistent across all projects).
- Private fields: `_camelCase`. Classes, methods, properties: `PascalCase`.
- File-scoped namespaces.
- Use `is null` / `is not null` for null checks.
- Inject interfaces from `Application/Contracts`, never concrete types from another layer.
  API endpoints inject `IMediator`, not repositories or handlers (see **`cqrs.md`**).
- Use `ILogger<T>` for logging. Use `async`/`await` and pass `CancellationToken` through
  database/API/service methods.
- Prefer readable explicit code. Avoid unnecessary abstractions. Do **not** create a
  repository interface for every class unless it provides genuine value (testability,
  crossing a layer boundary).

### Comments

Do **not** add comments that narrate what the code does. Write clear names and small
methods.

- No `//`, `///`, or block comments on new/changed production code unless the user asks.
- **Exceptional cases only:** non-obvious external constraints, security/legal
  requirements, or a temporary workaround — keep to one short line.
- Test methods use only the Arrange / Act / Assert section comments (see
  **`testing-csharp.md`**).

### Cyclomatic complexity

Keep **cyclomatic complexity ≤ 10 per method**. Prefer early returns and small private
helpers over long `if`/`switch` chains. Split a method before it exceeds 10.

### Braces and spacing (enforced via root `.editorconfig`)

| Construct | Brace position |
|-----------|----------------|
| `class`, `struct`, `record`, `interface`, `enum` | New line (Allman) |
| Methods, local functions | New line |
| `if`, `else`, `for`, `foreach`, `while`, `switch`, `try`/`catch`/`finally` | New line |
| Auto-properties (`{ get; set; }`) | Same line |
| Object/collection/array initializers | New line (Allman) |

Use **block bodies** for methods and local functions — not expression bodies (`=>`).
Run `dotnet format FishingLogBook.sln` after editing C#.

### Folder suffix naming (mandatory)

Types must be suffixed with the **singular** of their parent folder name. The **file
name must match the type name**.

| Folder | Suffix | Location | Examples |
|--------|--------|----------|----------|
| `Dtos/` | `Dto` | `FishingLogBook.Shared/Dtos/` | `HealthDto`, `DatabaseTestDto`, `TestRecordDto` |
| `Enums/` | `Enum` | Shared or Domain | `CatchMethodEnum` |
| `Constants/` | `Constants` | Shared | `CultureNames` stays until converted to `CultureConstants` |
| `Args/` | `Args` | `FishingLogBook.Application/Args/` | `FilterCatchesArgs` |

**Enums:** do not use `Type` or other suffixes in `Enums/` — use `Enum` only.

**DTO properties** may drop the `Enum` suffix when the meaning is clear (e.g. property
`Method` of type `CatchMethodEnum`).

## Domain layer (`FishingLogBook.Domain`)

- POCO entities only. No dependencies on other projects, no infrastructure concerns.

## Shared layer (`FishingLogBook.Shared`)

- API contracts shared over the wire, in `Shared/Dtos/` as `*Dto` records
  (e.g. `HealthDto`, `DatabaseTestDto`, `TestRecordDto`).
- Shared enums/constants follow folder suffix naming above.
- Do **not** place repositories, services, secrets, handlers, or server configuration here.

## Application layer (`FishingLogBook.Application`)

- CQRS vertical slices — same as rah-portal. Layout, naming, one-file command/query,
  `ValidatedResponse`, FluentValidation, Mapster, and the handler → `I*Service` →
  repository chain are in **`cqrs.md`**.
- Contracts: `Application/Contracts/Repositories/I*Repository` and
  `Application/Contracts/Services/I*Service`. Implementations of services live in
  `Application/Services/`; repositories in Infrastructure.
- This layer contains no DI registration of its own (see the composition root below).

## Infrastructure layer (`FishingLogBook.Infrastructure`)

- Dapper repositories in `Persistence/`; connection via `IDbConnectionFactory`.
- DbUp migrator in `Migrations/`.
- Parameterised SQL only — never string-concatenate values.
- This layer contains no DI registration of its own (see the composition root below).

## Composition root (`FishingLogBook.DependencyInjection`)

- Single place that wires up every layer, instead of a `DependencyInjection.cs` per
  project. Referenced only by the API (and any future host).
- `ServiceCollectionExtensions` exposes `AddFishingLogBook(IConfiguration)`, which composes
  `AddFishingLogBookApplication()` and `AddFishingLogBookInfrastructure(IConfiguration)`.
- `AddFishingLogBookApplication` registers MediatR (scan Application),
  `AddValidatorsFromAssembly`, `ValidationBehaviour<,>`, Mapster mappings, and
  application `I*Service` implementations. Register new repositories in
  `AddFishingLogBookInfrastructure`. Do not register individual handlers or validators.
- The Blazor host has its own composition method: `AddFishingLogBookWeb` in
  `FishingLogBook.Web`. Register new Web client services there, not only in `Program.cs`.

## Package versions

- Package versions are managed centrally with `Directory.Packages.props`
  (`ManagePackageVersionsCentrally`). Add or bump versions there, and reference packages in
  `.csproj` files **without** a `Version` attribute.

## API layer (`FishingLogBook.Api`)

- Minimal APIs (no controllers). Group endpoint mappings in `Endpoints/` extension methods
  (e.g. `MapSystemEndpoints`). Keep the Visual Studio `.http` scratch file in sync with
  those routes, grouped by the same area as the `Endpoints/` files.
- Keep endpoint handlers thin; send a MediatR command/query via `IMediator`;
  `IsFailure` → `Results.BadRequest`, else `Results.Ok` (see **`cqrs.md`**).
- Runtime health must reflect reality — never return a faked "healthy" response for a
  service that was not actually checked. If a dependency is unreachable, return an
  appropriate error status (e.g. 503).
- Connection strings and other configuration come from configuration/environment
  variables. Never commit credentials. Never log secrets, connection strings, or tokens.

## Before writing a new class

Read at least **5 existing classes of the same type** (endpoint mapping, command/query
file, repository, contract) before adding a new one, and match the established pattern.
