# FishingLogBook Architecture - Final Adjustments

Apply the following changes to the previously generated `docs/Architecture.md`.

## Web Hosting

Replace references to GitHub Pages as the preferred application host with:

### Web Application Hosting

The FishingLogBook Blazor WebAssembly PWA will initially be hosted using **Cloudflare Pages**.

GitHub will continue to provide:

- Source control
- Pull requests
- GitHub Actions
- Development and production deployment workflows

Cloudflare Pages is preferred over GitHub Pages because FishingLogBook is intended to become a commercial application.

The PWA must remain portable and must not depend on Cloudflare-specific runtime APIs for core application functionality.

---

## Object Storage

Use **Cloudflare R2** as the preferred initial object-storage provider for catch photographs.

Reasons include:

- S3-compatible API
- Low storage cost
- No standard internet egress charges
- Presigned upload support
- Straightforward migration to/from another S3-compatible provider

The application must access R2 through abstractions that do not expose Cloudflare-specific concepts to the domain or application layers.

Photographs must not be stored directly in PostgreSQL.

---

## Initial Hosting Architecture

The preferred initial architecture is:

```text
GitHub
   |
   +-- Source Control
   |
   +-- GitHub Actions
           |
           +---- Deploy PWA ------> Cloudflare Pages
           |
           +---- Deploy API ------> Fly.io

FishingLogBook PWA
       |
       +--------------------+
       |                    |
       v                    v
   Fly.io API          Local IndexedDB
       |
       +-----------+-------------+
       |                         |
       v                         v
 Neon PostgreSQL           Cloudflare R2

           Amazon Cognito
            Authentication
```

---

## Initial Providers

Use:

```text
Source Control      GitHub
CI/CD               GitHub Actions
PWA Hosting         Cloudflare Pages
API Hosting         Fly.io
Database            Neon PostgreSQL
Authentication      Amazon Cognito
Photo Storage       Cloudflare R2
Infrastructure      Terraform
```

These are initial hosting decisions and may be changed later without redesigning the core application.

---

## Infrastructure as Code Safety

Terraform is mandatory for infrastructure definition but must be applied **manually only**.

Terraform must NEVER automatically create, modify or destroy infrastructure as a result of:

- pushing code
- merging a pull request
- running a normal deployment pipeline
- creating a release

GitHub Actions may perform:

```text
terraform fmt -check
terraform init -backend=false
terraform validate
```

GitHub Actions must never perform:

```text
terraform apply
terraform destroy
```

Infrastructure deployment must follow:

```text
terraform init
terraform fmt
terraform validate
terraform plan -out=<environment>.tfplan
```

The developer must review the complete plan.

Only after deliberate review:

```text
terraform apply <environment>.tfplan
```

Development and production Terraform execution must remain separate.

---

## Infrastructure Cost Principle

No infrastructure resource should be created merely because it exists in the architecture diagram.

Terraform modules may describe future infrastructure, but resources should only be enabled when required.

The project must avoid automatic provisioning of:

- additional Fly.io machines
- paid database tiers
- load balancers
- background workers
- queues
- ML infrastructure
- dedicated networking
- redundant environments

unless explicitly approved.

A deployment problem must be diagnosed before increasing infrastructure size or adding resources.

---

## Environment Strategy

Support:

```text
Local
Dev
Prod
```

Local:

```text
Blazor PWA -> Local API -> Local PostgreSQL
```

Dev:

```text
Cloudflare Pages Dev
        |
        v
Fly.io Dev API
        |
        v
Neon Dev PostgreSQL

Cognito Dev
R2 Dev
```

Prod:

```text
Cloudflare Pages Prod
        |
        v
Fly.io Prod API
        |
        v
Neon Prod PostgreSQL

Cognito Prod
R2 Prod
```

Dev and Prod must not share operational databases.

Photo-storage namespaces/buckets must also be separated.

---

## CI/CD Responsibilities

GitHub Actions is responsible for **application deployment**, not infrastructure creation.

Permitted examples:

```text
Build solution
Run tests
Validate Terraform
Publish PWA
Deploy PWA to existing Cloudflare Pages project
Deploy API to existing Fly.io application
Run health checks
Run smoke tests
```

Not permitted:

```text
terraform apply
terraform destroy
Automatically create Fly.io applications
Automatically create databases
Automatically create R2 buckets
Automatically create Cognito resources
Automatically resize infrastructure
```

Cloud infrastructure must already exist before application deployment workflows execute.

---

## Initial Cost Expectations

FishingLogBook should initially operate with very low recurring infrastructure costs.

Cloudflare Pages should contribute little or no static hosting cost at initial usage.

Cloudflare R2 and Neon may remain within their free allowances during initial testing and low usage, but the project must never assume that a free tier guarantees zero cost.

Amazon Cognito provides sufficient initial free usage for a small MVP, but federated identity-provider usage and future pricing must be monitored.

The Fly.io API is expected to be the most predictable initial recurring compute cost.

Actual provider pricing must be reviewed periodically rather than copied permanently into architecture documentation.

---

## Localisation

UI language is a PWA concern.

- `IStringLocalizer<UiStrings>` with `.resx` resources in `FishingLogBook.Web`
- Default / fallback: `en-GB` (`UiStrings.resx`)
- French: `UiStrings.fr.resx`
- Culture is resolved from local storage, then the browser language, then `en-GB`
- Changing language reloads the PWA so satellite resources load
- MudBlazor uses a `MudLocalizer` that forwards to the same `IStringLocalizer`
- New screens must not hard-code user-visible English; add keys to both resource files
- API error payloads should prefer `errorCode` values that the client localises. Do not localise PostgreSQL data or Swagger
- ICU data is fully loaded (`BlazorWebAssemblyLoadAllGlobalizationData`) so the PWA can set `en-GB` or `fr` at startup. Blazor WASM does not allow a dynamic culture change with the default reduced ICU dataset.

---

## Cost Alerts

Where supported, configure billing alerts for:

```text
AWS
Fly.io
Neon
Cloudflare
```

Billing alerts are preferable to relying solely on provider free tiers.

Terraform must not automatically upgrade resources after thresholds are reached.

---

## First Architecture Milestone

The first cloud milestone must prove only:

```text
Cloudflare Pages
       |
       v
FishingLogBook PWA
       |
       v
Fly.io Dev API
       |
       v
Neon Dev PostgreSQL
```

The API must query a real DbUp-created test table.

Cognito and R2 infrastructure may also be created during the foundation phase but must not cause the initial vertical-slice test to become unnecessarily complicated.

---

## Second Architecture Milestone

After the basic cloud path is proven, priority moves immediately to the largest technical risk:

```text
Offline PWA
    |
    v
IndexedDB
    |
    v
Offline Catch + Photograph
    |
connectivity returns
    |
    v
Synchronisation
    |
    +----> Fly.io API
    |
    +----> R2 photograph upload
    |
    +----> Neon metadata
```

This must be tested on real Android and iPhone devices before substantial feature development proceeds.

Location metadata is part of that offline Catch path (see `docs/Requirements.md` §32 and §44). Architecture should later specify how optional coordinates, accuracy, source, visibility and consent travel with the catch through IndexedDB and synchronisation, without blocking save when location is unavailable. Mapping providers, heat maps and aggregated location analytics are not part of the first or second architecture milestone.

---

## Architecture Rule

The architecture should optimise for:

```text
Reliable first
Cheap second
Simple third
Scalable when required
```

Do not design infrastructure for hypothetical scale before real usage demonstrates a need.