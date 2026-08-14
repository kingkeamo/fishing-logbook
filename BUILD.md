# FishingLogBook

## Initial Build Instructions

**Document:** BUILD.md  
**Purpose:** Explicit implementation instructions for the initial FishingLogBook technical foundation.

This document must be read together with:

- `docs/Requirements.md`
- `docs/Architecture.md`

The initial objective is not to build the complete FishingLogBook MVP.

The initial objective is to prove the complete technical architecture cheaply and safely before significant product functionality is implemented.

---

# 1. Critical Infrastructure Safety Rule

Terraform must NOT be automatically applied by GitHub Actions.

This is a mandatory project rule.

GitHub Actions may run:

```text
terraform fmt -check
terraform init -backend=false
terraform validate
```

GitHub Actions must NOT run:

```text
terraform apply
terraform destroy
terraform import
```

No CI/CD workflow may create, resize, modify or destroy cloud infrastructure.

Infrastructure changes must be executed manually by a developer from a local machine after reviewing the Terraform plan.

The normal Terraform workflow is:

```text
terraform init
terraform fmt
terraform validate
terraform plan
```

Developer reviews the plan.

Only after deliberate review:

```text
terraform apply
```

Production infrastructure must never be created or modified merely because code was pushed or merged.

---

# 2. Cost Safety

Infrastructure must initially use the smallest practical resources.

Do not provision:

- Multiple API machines by default
- Large Fly.io machines
- Paid PostgreSQL plans without an explicit decision
- Multiple production databases unnecessarily
- NAT gateways
- Load balancers
- Kubernetes
- Reserved compute
- ML infrastructure
- Queues or workers before needed

Terraform variables must make resource sizing explicit.

Infrastructure documentation must identify anything that can incur recurring cost.

Before applying Terraform, the developer should be able to understand from the plan which resources will be created.

---

# 3. Initial Hosting Decisions

Use the following initial providers.

## Source Control

GitHub.

## CI/CD

GitHub Actions.

## PWA Hosting

Cloudflare Pages.

Do not use GitHub Pages for the production FishingLogBook application.

## API Hosting

Fly.io.

## PostgreSQL

Neon PostgreSQL.

## Authentication

Amazon Cognito.

## Photograph/Object Storage

Cloudflare R2.

## Infrastructure as Code

Terraform, manually applied only.

---

# 4. Repository Location

The expected local repository will be:

```text
C:\git\fishing-logbook
```

Do not make code depend on this absolute path.

---

# 5. Required Repository Structure

Create the following structure:

```text
fishing-logbook/
│
├── src/
│   ├── FishingLogBook.Api/
│   ├── FishingLogBook.Application/
│   ├── FishingLogBook.Db.Migrations/          # DbUp SQL scripts (embedded) + migration engine
│   ├── FishingLogBook.Db.Migrations.App/      # Console migration runner (local + pipeline)
│   ├── FishingLogBook.DependencyInjection/    # Composition root (AddFishingLogBook)
│   ├── FishingLogBook.Domain/
│   ├── FishingLogBook.Infrastructure/
│   ├── FishingLogBook.Shared/
│   └── FishingLogBook.Web/
│
├── tests/                                     # One test project per production project
│   ├── FishingLogBook.Tests.Common/           # Shared builders/fixtures (no tests)
│   ├── FishingLogBook.Shared.Tests/
│   ├── FishingLogBook.Application.Tests/
│   ├── FishingLogBook.Infrastructure.Tests/
│   ├── FishingLogBook.Db.Migrations.Tests/
│   ├── FishingLogBook.Api.Tests/
│   └── FishingLogBook.Web.Tests/
│
├── infrastructure/
│   ├── fly/                                   # flyctl app config (not Terraform)
│   └── terraform/
│       ├── modules/
│       └── environments/
│           ├── dev/
│           └── prod/
│
├── .github/
│   └── workflows/
│
├── docs/
│   ├── Requirements.md
│   └── Architecture.md
│
├── BUILD.md
├── README.md
├── Directory.Packages.props                   # Central Package Management (versions)
├── Dockerfile
├── .gitignore
└── FishingLogBook.sln
```

> SQL migration scripts live **inside** `FishingLogBook.Db.Migrations` (under numbered
> folders), not in a top-level `database/` folder.

---

# 6. .NET Version

Use the current selected project .NET version consistently across all projects.

Do not mix framework versions between projects.

Before changing the target framework from the initial selection, document the reason.

---

# 7. Solution Projects

Create:

```text
FishingLogBook.Api
FishingLogBook.Application
FishingLogBook.Db.Migrations
FishingLogBook.Db.Migrations.App
FishingLogBook.DependencyInjection
FishingLogBook.Domain
FishingLogBook.Infrastructure
FishingLogBook.Shared
FishingLogBook.Web
```

Create test projects (one per production project, plus a shared helper library):

```text
FishingLogBook.Tests.Common
FishingLogBook.Shared.Tests
FishingLogBook.Application.Tests
FishingLogBook.Infrastructure.Tests
FishingLogBook.Db.Migrations.Tests
FishingLogBook.Api.Tests
FishingLogBook.Web.Tests
```

Add all projects to:

```text
FishingLogBook.sln
```

NuGet package versions are managed centrally via `Directory.Packages.props` (Central
Package Management) — `.csproj` files reference packages without a `Version` attribute.

Tests use xUnit + NSubstitute + **AwesomeAssertions** (the Apache-2.0 fork of
FluentAssertions) + bUnit, and follow the `WhenTesting` naming convention documented in
`.claude/rules/testing-csharp.md`.

---

# 8. Project Dependency Rules

Use these dependency rules.

```text
FishingLogBook.Domain
    no project dependencies

FishingLogBook.Application
    -> FishingLogBook.Domain
    -> FishingLogBook.Shared where required

FishingLogBook.Infrastructure
    -> FishingLogBook.Application
    -> FishingLogBook.Domain

FishingLogBook.DependencyInjection   (composition root)
    -> FishingLogBook.Application
    -> FishingLogBook.Infrastructure

FishingLogBook.Db.Migrations         (standalone; embeds SQL scripts)
    no project dependencies

FishingLogBook.Db.Migrations.App     (console migration runner)
    -> FishingLogBook.Db.Migrations

FishingLogBook.Api
    -> FishingLogBook.Application
    -> FishingLogBook.DependencyInjection
    -> FishingLogBook.Shared

FishingLogBook.Web
    -> FishingLogBook.Shared only
```

The API composes its services through the `FishingLogBook.DependencyInjection` composition
root (`AddFishingLogBook`) rather than referencing `Infrastructure` directly. Migrations are
a standalone concern (`FishingLogBook.Db.Migrations` + runner) and are not referenced by the
API.

The Blazor WebAssembly project must NOT reference:

```text
FishingLogBook.Application
FishingLogBook.Infrastructure
```

Do not expose server-side implementation assemblies to the WebAssembly client.

---

# 9. Shared Project

`FishingLogBook.Shared` will contain API contracts that genuinely need to be shared between the API and Web client.

Examples:

```text
HealthDto
DatabaseTestDto
TestRecordDto
```

Later this may contain:

```text
CatchDto
CreateCatchRequest
FisheryDto
GuideDto
```

Do not place:

- database repositories
- MediatR handlers
- infrastructure services
- secrets
- server configuration

inside Shared.

---

# 10. Blazor PWA

Create `FishingLogBook.Web` as a Blazor WebAssembly Progressive Web Application.

Use MudBlazor.

The initial PWA must include:

- Web manifest
- Service worker
- Offline-capable application shell
- Light theme
- Dark theme
- Mobile-first layout
- Responsive desktop layout
- Localisation (`IStringLocalizer` + `.resx`) for English (`en-GB`) and French (`fr`)

Do not build the full FishingLogBook user interface yet.

UI copy must go through resource keys. Do not hard-code user-visible English in `.razor` files.

---

# 11. Web Project Structure

Use:

```text
Pages/
Components/
Layouts/
Services/
Localization/
Offline/
Models/
wwwroot/
```

Each significant page should use:

```text
PageName.razor
PageName.razor.cs
PageName.razor.css
```

Do not place large code blocks inside `.razor` files.

---

# 12. Initial UI

Create a simple mobile-first landing/test page.

It should display:

```text
FishingLogBook

Web: Online

API: Checking...

Database: Checking...
```

Once loaded successfully it should display:

```text
Web: Online
API: Online
Database: Online
```

The page exists only to prove the vertical architecture.

Use MudBlazor components and ensure it works in light and dark mode.

---

# 13. API

Create an ASP.NET Core API.

The API must initially expose:

```text
GET /health
```

Response example:

```json
{
  "status": "Healthy"
}
```

Also create:

```text
GET /api/system/database
```

This endpoint must perform a real database query rather than merely returning a configured status.

---

# 14. Test Database Table

Create an initial database table using DbUp.

Table name:

```text
SystemTest
```

Do not use underscores in database table names.

Suggested columns:

```text
Id
Name
CreatedOn
```

Insert one seed/test record through a migration.

Example:

```text
Id: generated UUID
Name: FishingLogBook database online
```

---

# 15. DbUp

Use DbUp for migrations, in **two dedicated projects** (separate from the API and
Infrastructure):

- `FishingLogBook.Db.Migrations` — holds the SQL scripts (embedded) and the DbUp engine
  (`MigrationService`, `FilenameOnlyScriptComparer`, `PostgresDatabaseHelper`).
- `FishingLogBook.Db.Migrations.App` — a console runner used to apply migrations locally,
  in a pipeline, or ad hoc.

Scripts live under numbered folders inside `FishingLogBook.Db.Migrations`:

```text
01_Tables/     02_SeedData/     03_Routines/     04_Scripts/
```

Filename convention: `YYYYMMDDHHMM_{GitHubIssue}_{Description}.sql` (no `#`).
Example: `202608141200_3_AddCatchTable.sql` for issue `#3`. Initial scripts (already
applied — do not rename):

```text
01_Tables/202608131200_CreateSystemTest.sql
02_SeedData/202608131201_SeedSystemTest.sql
```

Scripts are **ordered by filename only** (via `FilenameOnlyScriptComparer` /
`WithScriptNameComparer`), so the timestamp prefix determines run order across all folders —
a script authored earlier always runs first, regardless of folder. DbUp records applied
scripts in its `SchemaVersions` journal and runs each once.

Do not use EF Core migrations.

Migration execution must be explicit and logged. The **API does not run migrations on
startup** — migrations are applied by the runner:

```text
# Interactive (local)
dotnet run --project src/FishingLogBook.Db.Migrations.App

# Non-interactive (CI/pipeline)
dotnet run --project src/FishingLogBook.Db.Migrations.App -- --run
```

The runner reads `Db:ConnectionString` (user secrets, `Db__ConnectionString` env var, or a
local `appsettings.Development.json`).

---

# 16. Database Connectivity Endpoint

`GET /api/system/database` must:

1. Connect to PostgreSQL.
2. Query `SystemTest`.
3. Return the test record.

Example:

```json
{
  "status": "Healthy",
  "name": "FishingLogBook database online"
}
```

If the database cannot be reached, return an appropriate error status rather than a fake successful response.

Do not hard-code successful database responses.

---

# 17. Database Access

Use PostgreSQL and Dapper.

Do not introduce Entity Framework.

Queries must be parameterised.

Connection strings must come from configuration/environment variables.

Never commit live database credentials.

---

# 18. Local Development Database

Support local PostgreSQL development.

Provide documented configuration for:

```text
FishingLogBook local API
    ->
local PostgreSQL
```

A developer should be able to run the system locally without requiring the Dev Neon database.

---

# 19. Neon Development Database

Terraform must define a Dev Neon database where supported by the current Terraform provider.

The API Dev environment should use the Neon Dev database.

Production must use a separate PostgreSQL database/project or otherwise appropriately isolated database.

Never point Development at Production.

---

# 20. API Container

Create a Dockerfile for `FishingLogBook.Api`.

Use a multi-stage .NET build.

The resulting container should:

- run as safely as practical
- expose the expected application port
- accept configuration through environment variables
- contain no embedded secrets

Keep the container provider-neutral.

---

# 21. Fly.io

Create Fly.io configuration for:

```text
FishingLogBook API Dev
FishingLogBook API Prod
```

Do not deploy two machines per environment unless explicitly requested.

Begin with the smallest practical Fly.io machine.

Do not enable automatic horizontal scaling initially.

Dev and Prod must have separate Fly applications.

---

# 22. API Deployment

GitHub Actions may deploy application code to an already-existing Fly.io application.

This distinction is critical:

```text
Allowed from CI:
Deploy new API container/application version

Not allowed from CI:
Create Fly infrastructure using Terraform
Resize infrastructure using Terraform
Destroy infrastructure
```

The workflow may use `flyctl deploy` against a pre-existing application.

---

# 23. Cloudflare Pages

Deploy the Blazor PWA to Cloudflare Pages.

The deployment must support:

```text
Dev
Prod
```

Prefer distinct Cloudflare Pages projects or clearly isolated deployment targets.

Configuration must be environment-specific.

Do not hard-code API URLs in source code.

---

# 24. PWA Environment Configuration

Support:

```text
Local
Dev
Prod
```

Local Web should call the local API.

Dev Web should call the Fly.io Dev API.

Prod Web should call the Fly.io Prod API.

Use environment-specific public configuration.

Remember that all Blazor WASM configuration sent to the browser is public.

Do not place secrets in:

```text
appsettings.json
appsettings.Development.json
appsettings.Production.json
JavaScript
Blazor assemblies
```

---

# 25. Authentication Infrastructure

Terraform should define Amazon Cognito infrastructure.

Initially define:

```text
Dev User Pool
Dev App Client
Prod User Pool
Prod App Client
```

or an equivalent clearly isolated environment design.

Use Authorization Code flow with PKCE for the PWA.

Do not use a client secret in the Blazor application.

External identity providers do not need to be implemented in the first vertical slice, but the Cognito configuration must allow them to be added later.

---

# 26. Internal User IDs

Do not make Cognito's subject identifier the primary domain key.

FishingLogBook should eventually have its own:

```text
UserId
```

Cognito identity should be mapped to that user.

The first technical milestone does not require the full User domain implementation.

---

# 27. Cloudflare R2

Terraform should define separate R2 storage for Dev and Prod where practical.

Example:

```text
fishing-logbook-dev
fishing-logbook-prod
```

Do not make either bucket publicly writable.

Do not store R2 secret credentials in the PWA.

---

# 28. R2 Initial Test

The first vertical slice does not need full catch photograph functionality.

However, add an infrastructure smoke-test plan documenting how R2 connectivity will be tested during the next milestone.

Do not upload production photographs during initial infrastructure testing.

---

# 29. Future Photo Upload Model

Design interfaces around:

```text
PWA
    ->
API requests upload URL
    ->
API generates short-lived R2 presigned URL
    ->
PWA uploads directly to R2
    ->
PWA/API records metadata
```

Do not proxy all full-size photo bytes through the API unless there is a specific reason.

---

# 30. Terraform Structure

Create:

```text
infrastructure/terraform/
│
├── modules/
│   ├── cognito/
│   ├── neon/
│   ├── fly/
│   ├── r2/
│   └── cloudflare-pages/
│
└── environments/
    ├── dev/
    │   ├── main.tf
    │   ├── variables.tf
    │   ├── outputs.tf
    │   └── terraform.tfvars.example
    │
    └── prod/
        ├── main.tf
        ├── variables.tf
        ├── outputs.tf
        └── terraform.tfvars.example
```

Exact modules may be adjusted if a provider does not safely support a particular resource.

Do not invent unsupported Terraform resources merely to satisfy this structure.

Document anything that requires manual provider setup.

---

# 31. Terraform State

Do not commit Terraform state files.

Add to `.gitignore`:

```text
*.tfstate
*.tfstate.*
.terraform/
terraform.tfvars
```

Do not commit credentials.

For the earliest local prototype, local state is acceptable if carefully handled.

Before multiple developers or serious Production infrastructure management, configure an appropriate remote encrypted state backend.

---

# 32. Terraform Commands

Create documentation for manual deployment.

Example Dev process:

```text
cd infrastructure/terraform/environments/dev

terraform init
terraform fmt
terraform validate
terraform plan -out=dev.tfplan
```

Developer reviews the entire plan.

Then deliberately:

```text
terraform apply dev.tfplan
```

Production follows an equivalent process.

Do not provide a one-command script that automatically plans and applies without review.

---

# 33. GitHub Actions

Create the following initial workflows:

```text
build-test.yml
deploy-api.yml
deploy-web.yml
```

Optionally:

```text
terraform-validate.yml
```

There must be no Terraform apply workflow.

---

# 34. Build/Test Workflow

`build-test.yml` should run on pull requests and relevant pushes.

It should:

```text
dotnet restore
dotnet build
dotnet test
```

It should also validate Terraform:

```text
terraform fmt -check
terraform init -backend=false
terraform validate
```

Validation only.

---

# 35. API Deployment Workflow

`deploy-api.yml` should:

1. Build/test or depend upon successful build validation.
2. Deploy to the already-created Fly.io environment.
3. Wait for deployment.
4. Call `/health`.
5. Call `/api/system/database`.
6. Fail if either check fails.

Dev and Production deployment must use separate GitHub environments/secrets.

Production deployment must require deliberate workflow selection or approval.

---

# 36. Web Deployment Workflow

`deploy-web.yml` should:

1. Build the Blazor WASM PWA.
2. Apply environment-specific public configuration.
3. Deploy static assets to the correct Cloudflare Pages environment/project.
4. Request the deployed web URL.
5. Verify it returns successfully.
6. Verify the PWA can call the appropriate API.

Do not deploy the Web application if compilation fails.

---

# 37. GitHub Environment Separation

Configure GitHub environments:

```text
dev
prod
```

Environment-specific configuration should include only values needed by deployment workflows.

Sensitive values must be GitHub Secrets.

Non-sensitive values may be GitHub Variables.

Do not store cloud secrets directly in workflow YAML.

---

# 38. Cost Protection

Where supported, configure provider budgets/alerts manually or with Terraform when safe.

At minimum document cost alerts for:

- AWS
- Fly.io
- Neon
- Cloudflare

Do not assume that a free tier prevents accidental charges.

Do not provision additional resources to solve a deployment error without first identifying the cause.

---

# 39. README Cost Warning

The infrastructure README must contain this prominent warning:

```text
Terraform is intentionally manual.

Never run terraform apply without reviewing the complete plan.

Never add terraform apply or terraform destroy to GitHub Actions.

Cloud resources may incur charges immediately after creation.
```

---

# 40. Initial Smoke Test

After manually creating Dev infrastructure and deploying application code, verify:

```text
Web URL
    ->
FishingLogBook PWA loads

PWA
    ->
GET Fly Dev /health
    ->
Healthy

PWA
    ->
GET Fly Dev /api/system/database
    ->
Neon query
    ->
SystemTest record
```

The UI must display the actual API/database result.

No fake values.

---

# 41. Local Vertical Slice

The same flow must work locally:

```text
Local Blazor PWA
    ->
Local API
    ->
Local PostgreSQL
```

Do not require cloud infrastructure to perform normal local development.

---

# 42. Initial Automated Tests

Add tests covering at least:

- `/health`
- database system endpoint
- database repository test where practical
- Shared DTO serialisation
- initial Web status component

Do not create hundreds of meaningless tests simply to increase coverage.

---

# 43. Code Quality

Enable:

```text
Nullable
ImplicitUsings
```

Use asynchronous database/API methods.

Use cancellation tokens where appropriate.

Avoid unnecessary abstractions.

Do not create repository interfaces for every class unless they provide genuine value.

Prefer readable explicit code.

---

# 44. No Fake Implementations

If an external system is not configured, report it clearly.

Do not return invented:

```text
database healthy
storage healthy
authentication healthy
```

responses without actually checking the relevant service.

Mocks are appropriate inside automated tests only.

Runtime application health must reflect reality.

---

# 45. Logging

Use structured ASP.NET Core logging.

During initial implementation log:

- Application startup
- DbUp migration execution
- Database connectivity failures
- API errors

Never log:

- passwords
- connection strings
- access tokens
- refresh tokens
- Cognito credentials
- R2 secrets

---

# 46. First Commit Goal

The first substantial implementation commit should contain:

- Solution structure
- Projects
- Central Package Management (`Directory.Packages.props`)
- Dependency injection composition root (`FishingLogBook.DependencyInjection`)
- Buildable Web PWA
- Buildable API
- Shared project
- Per-project test projects + shared `Tests.Common` helpers
- DbUp migration projects (library + console runner)
- Initial SQL migrations
- Dockerfile
- Terraform structure
- GitHub validation workflow
- Documentation

It does not need to contain fisheries, guides, competitions or real catch functionality yet.

---

# 47. First Cloud Milestone

After manually applying Dev Terraform, achieve:

```text
Cloudflare Pages PWA online
Fly.io Dev API online
Neon Dev PostgreSQL online
Cognito Dev infrastructure created
R2 Dev bucket created
```

Then prove:

```text
Browser -> PWA -> API -> PostgreSQL
```

Do not progress to significant MVP functionality until this works reliably.

---

# 48. Second Milestone: Offline Proof

Once the first milestone works, implement only enough catch functionality to prove offline behaviour.

Create a minimal TestCatch containing:

```text
Id
SpeciesName
CaughtOn
Notes
SyncStatus
```

Store it locally using IndexedDB.

Test:

1. Load application online.
2. Disconnect network.
3. Create TestCatch.
4. Save to IndexedDB.
5. Close PWA.
6. Reopen PWA.
7. Confirm TestCatch still exists.
8. Reconnect.
9. Synchronise to API.
10. Confirm PostgreSQL record exists.

---

# 49. Third Milestone: Offline Photograph Proof

After metadata sync works, prove photographs.

Test:

1. Disconnect device.
2. Capture/select photograph.
3. Store photograph locally.
4. Associate it with TestCatch.
5. Close app.
6. Reopen app.
7. Confirm photograph remains available.
8. Restore network.
9. Request R2 presigned upload URL.
10. Upload photograph directly to R2.
11. Record photograph metadata.
12. Confirm it is viewable after loading the catch on another device.

This is the most important technical proof before building the real catch experience.

---

# 50. Real Device Requirement

The offline milestones must be tested on:

```text
Android Chrome / installed PWA
iPhone Safari / Home Screen PWA
```

Desktop browser testing alone is not sufficient.

Document any iOS limitations discovered.

---

# 51. Do Not Build Yet

Until the technical milestones above are proven, do not implement:

- Full competitions
- Booking engine
- Payment processing
- AI fish recognition
- Guide marketplace
- Advanced fishery search
- Social features
- Messaging
- Weather
- Tides

The architecture foundation and offline/photo workflow come first.

---

# 52. Definition of Initial Build Complete

The initial technical build is complete when:

- Solution builds locally.
- Tests pass.
- Terraform validates.
- Terraform is manually deployable.
- No Terraform apply exists in CI/CD.
- Local PWA works.
- Local API works.
- Local PostgreSQL works.
- Dev Cloudflare Pages PWA works.
- Dev Fly.io API works.
- Dev Neon database works.
- API reads a real migrated table.
- Cognito Dev infrastructure exists.
- R2 Dev storage exists.
- API and Web deployments are automated after infrastructure already exists.
- Production infrastructure remains deliberate/manual.
- Costs and resource sizes are documented.

At this point begin the offline catch proof before implementing the wider MVP.