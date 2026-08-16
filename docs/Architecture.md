# FishingLogBook Architecture

**Document Status:** Architecture aligned with current Requirements  
**Must be read with:** `docs/Requirements.md` and `BUILD.md`

This document defines how FishingLogBook is hosted and how Catch, location, photographs, synchronisation, FishingVenue and Club must be designed. It does not implement those product features in code, schema or infrastructure.

Technology and hosting decisions belong here rather than in the product requirements.

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

---

## Application Architecture

FishingLogBook is a Blazor WebAssembly PWA talking to an ASP.NET Core API. The API uses Dapper against PostgreSQL. Catch recording is offline-first: the PWA must be able to photograph and save a catch with no network.

### Layers

```text
FishingLogBook.Web          Blazor WASM PWA (MudBlazor). References Shared only.
        |
        v
FishingLogBook.Shared       DTOs and contracts used by Web and API.
        |
        v
FishingLogBook.Api          Minimal APIs. Composes services via DependencyInjection.
        |
        v
FishingLogBook.Application  Use cases. No infrastructure types.
        |
        +--> FishingLogBook.Domain          Entities and domain rules. No project dependencies.
        |
        v
FishingLogBook.Infrastructure   PostgreSQL (Dapper), object-storage abstractions.

FishingLogBook.DependencyInjection   Composition root (`AddFishingLogBook`).
FishingLogBook.Db.Migrations         DbUp scripts and engine. Not referenced by the API.
```

The Blazor client must not reference Application or Infrastructure. Server implementation assemblies must not ship to the browser.

New Application use cases use MediatR 12.5.0 CQRS: one file per command/query (request,
handler, `ValidatedResponse`, validator). The API translates a validated access token
into `Provider`, `Subject`, and authenticated `Email` and sends
`ResolveCurrentUserCommand`. Email is account data, not the identity lookup key.
The FishingLogBook API requires that trusted `email` claim on the access token together
with `sub`. Cognito access tokens include Email via a Pre Token Generation Lambda
(event version V2_0) that copies the verified email user attribute. JWT validation
is unchanged. Tokens include Email only after that Lambda is applied in the
environment.

Handlers call application services; services call repositories. Repositories own SQL
transactions.

Distinguish these identities:

- Authentication: Cognito validates the external identity.
- External identity: `Provider` + `Subject` (`UserIdentity`).
- Product identity: FishingLogBook `User.Id`.
- Account data: `User.Email` (required, mutable, not unique).
- Request identity: `ICurrentUser` — request-scoped Application indication of which
  Domain `User` is authenticated. Not a Domain entity.
- Domain entity: `User`.

After identity resolution, application code uses `ICurrentUser.UserId` (and
`ICurrentUser.Email` for account data), not Cognito `sub`. Do not pass `HttpContext`,
`ClaimsPrincipal`, or JWT types into Application.

The PWA presents the authenticated OIDC email claim in the app bar. That is display
only. Server ownership remains validated access token → Provider + Subject → UserId.

Internal user identity is a FishingLogBook `UserId`. Cognito authenticates the person
and supplies `sub`; that value is stored only as an external `UserIdentity`
(`Provider` + `Subject`) and is not the domain key. `UNIQUE (Provider, Subject)` is
the lookup key. `User.Email` is required account data: mutable, not unique, and never
used to find, resolve, merge, or prove ownership of users. Two different
Provider + Subject identities may share the same Email and remain different Users.
Domain records use internal `UserId`. The first authenticated API interaction creates
or resolves the mapping and stores Email; later requests reuse that UserId and refresh
Email when the authenticated claim changes.
The API derives the current user from the validated access token; it does not trust a
client-supplied UserId or email. Offline device-generated ids are for synchronisation
idempotency, not ownership. Future profile work may add FirstName, LastName, and
DisplayName.

### Offline client storage

Unsynchronised catches, photographs and any captured location live in the PWA (IndexedDB). Local data is authoritative until synchronisation succeeds. Closing or restarting the application must not lose an offline catch within reasonable browser limitations.

---

## Catch

Catch logging is the primary product feature (`docs/Requirements.md` # 5). Architecture must treat Catch as an offline-first aggregate, not as an online-only API record.

A Catch conceptually supports:

- One or more photographs
- Species, weight, length, date, time
- Optional captured location (see **Location and Privacy**)
- FishingVenue association, independent of captured location
- Fishing method, bait or lure, notes
- Angler (the person who caught the fish)
- Person who recorded the catch
- Guide trip, competition and club association where applicable

Not every field is mandatory. **Location is never mandatory.** A catch must be saveable when location is missing, denied, inaccurate or unavailable.

Each offline-created Catch must have a unique identifier generated on the device. That identifier is the idempotency key for synchronisation.

The model must distinguish permanently:

- the person who caught the fish
- the person who recorded the catch

For a guided trip these may differ. Provenance is retained after synchronisation.

Do not prescribe database column names in this document. Domain and API names may differ from storage names.

---

## Location and Privacy

Location is part of the initial MVP Catch model (`docs/Requirements.md` # 33). Capture is optional. Failure to obtain location must **never** prevent a catch from being saved.

Exact fishing spots are sensitive. Enabling device location, joining a club, or associating a catch with a FishingVenue must not by itself make coordinates visible to other users.

### Capture and sharing are separate

There is an explicit distinction between:

1. Permission to **capture and store** location.
2. Permission to **share or expose** that location.

A user may allow FishingLogBook to record precise coordinates for their private fishing history without allowing those coordinates to be visible to other users.

Precise coordinates are **private by default**.

Potential visibility levels include:

- Private
- Approximate area
- Fishing venue only
- Public

The model must be able to evolve without forcing exact coordinates to be exposed. Granting location permission must not imply that the location is public.

### What to retain with a captured location

When location is captured, retain conceptually equivalent information to:

- Latitude and longitude
- Location accuracy in metres, where the device supplies it
- Date/time the location was captured
- Location source
- Location visibility
- Location consent version (or equivalent)

Do not treat this list as prescribed database column names.

`LocationSource` must distinguish origin, including:

- Device GPS / location services
- Manual user selection
- Fishing venue
- Other future location sources

A FishingVenue association must **not** be treated as equivalent to an accurate device GPS coordinate.

The system must not treat all coordinates as equally accurate. Future mapping and analytics must be able to account for accuracy. Mapping providers, personal catch maps, heat maps and aggregated location analytics are **not** part of the first or second architecture milestone and are not required for MVP UI.

### FishingVenue and GPS are independent

A Catch must be capable of independently having:

- a FishingVenue association
- a precise or approximate captured location

Either may exist without the other.

Examples:

```text
FishingVenue = Lough Corrib, GPS = none     (venue selected, GPS refused)
FishingVenue = none, GPS = available        (unregistered water, GPS allowed)
FishingVenue = Lough Corrib, GPS = available
```

Club association, FishingVenue association and precise GPS location remain separate concepts.

### Device permission and offline capture

The PWA requests location permission explicitly where the platform requires it. Explain the benefit before requesting permission. Handle granted, denied and unavailable outcomes without blocking save.

If the user denies permission:

- Continue catch logging normally.
- Do not repeatedly interrupt or nag.
- Allow permission to be enabled later.
- Allow location to be associated manually later where appropriate.

When a catch is created offline:

1. Attempt to obtain location if permission exists.
2. Store any captured location with the local catch.
3. Preserve it while the catch remains in IndexedDB.
4. Synchronise location metadata with the catch when connectivity returns.

Location capture must work independently of API availability where the device or browser can provide it. Failure to obtain location while offline must not affect catch creation.

Full manual add/correct/remove location UX is not required for the initial MVP UI. The Catch/location model must not prevent it. Manual selection must remain distinguishable from device GPS.

### Authorisation for coordinates

Public catch views, club views, guide views and FishingVenue views must respect the catch owner's location visibility settings.

Being any of the following must **not** automatically grant access to an angler's private precise coordinates:

- ClubAdmin
- ClubOfficer
- ClubCompetitionOrganiser
- Guide
- FishingVenue manager
- CompetitionOrganiser

Where a competition genuinely requires location verification, coordinates may be made available specifically for verification according to the competition rules and user consent, without automatically making them public.

API authorisation must enforce these rules. Privileged roles are not a back door to private coordinates.

A catch associated with a club-managed FishingVenue may later contribute to permitted aggregate club statistics. That must **not** mean club administrators receive precise coordinates, private notes, unrelated catches, or personal data not required for the club use case.

---

## Photographs

Users must be able to take a photograph, select an existing photograph, attach multiple photographs to a catch, and review them before saving (`docs/Requirements.md` # 6).

Photographs captured offline must remain available until successfully uploaded. Closing the PWA must not lose unsynchronised photographs within reasonable browser limitations.

Photographs must **not** be stored directly in PostgreSQL. PostgreSQL holds photograph metadata after a successful upload. Image bytes go to object storage.

The preferred upload path is:

```text
PWA stores the photograph locally
    ->
API requests a short-lived upload URL
    ->
API generates a presigned R2 URL
    ->
PWA uploads directly to R2
    ->
PWA/API records photograph metadata
```

Do not proxy all full-size photo bytes through the API unless there is a specific reason.

Photograph uploads must be able to retry independently of catch metadata. Location metadata captured with an offline catch must synchronise with that catch and must not be dropped if photograph upload is retried separately.

The application must access object storage through abstractions that do not expose Cloudflare-specific concepts to the domain or application layers.

Preserve sufficient image quality for future fish identification while balancing storage and upload size. Individual fish AI recognition and automatic species recognition are not MVP.

---

## Synchronisation

The system must treat local data as authoritative until successfully synchronised (`docs/Requirements.md` # 34).

Rules:

- Each offline-created record has a unique device-generated identifier.
- Retrying the same Catch must not create a duplicate server record.
- The user must be able to see whether a catch is saved locally, waiting to synchronise, synchronising, successfully synchronised, or failed to synchronise.
- A manual retry/synchronise option must be provided.
- Network failure during synchronisation must not result in data loss.
- Catch metadata, location metadata and photographs may complete at different times. Location must travel with the catch even if a photograph is still uploading.
- Once synchronised, a catch should be available on the user's other devices.
- The MVP does not need sophisticated simultaneous offline-edit conflict resolution. Conflicts must never silently cause a catch to disappear.

---

## FishingVenue

The underlying venue domain is **FishingVenue** (`docs/Requirements.md` # 11). A FishingVenue may represent a fishery, lake, river, river section, canal, reservoir, commercial fishery, other managed water, or coastal/sea-fishing location where appropriate.

The domain is not restricted to commercial fisheries. The UI may use context-appropriate language such as Fishery, Water, Lake, River or Venue. Do not force a single user-facing label where a more natural term applies.

A venue does not need to have joined FishingLogBook before appearing in the directory. Users can record catches against a venue. Owners or authorised managers may later claim an existing venue profile.

A venue must be able to exist independently of a club. Do not duplicate venue records for club use. A venue may be associated with more than one organisation where business rules permit.

Season, opening, closure, rules, facilities, catch and competition functionality operate against the venue. Club-level rules and venue-level rules must remain distinguishable.

FishingVenue is **not** GPS. Associating a catch with a venue does not create or replace device coordinates.

---

## Club

A fishing club is an **organisation**, not a user account (`docs/Requirements.md` # 3 and # 22).

Clubs integrate into the existing model for anglers, fishing venues, guides and competitions. They are not a disconnected club-administration subsystem. FishingLogBook must not become a general-purpose club admin product.

### Membership and roles

A normal user may belong to one or more clubs and may hold club-specific roles on those clubs.

A club membership conceptually supports:

- User
- Club
- Membership number/reference
- Membership type
- Membership start and end dates
- Membership status
- Joined date

Membership statuses should support at least Pending, Active, Expired, Suspended and Cancelled.

Membership types should be extensible (for example Adult, Junior, Senior, Family, Student, Guest). Do not hard-code these as permanent product-wide enums if clubs may later configure their own types.

Club-scoped role examples:

- ClubMember
- ClubAdmin
- ClubOfficer
- ClubCompetitionOrganiser

These permissions are scoped to an **individual club**. Being an administrator of Club A must not grant administrative permissions over Club B.

Club-scoped capabilities sit alongside platform-level capabilities (Angler, Guide, FishingVenue manager, Competition Organiser, Administrator) and must not be mutually exclusive with them. Do not create a separate account type for club officers.

### Waters, rules and competitions

A club may own, lease, manage or have fishing rights over one or more FishingVenues. Display those waters on the club profile without duplicating venue records.

Club-level rules and venue-level rules must be distinguishable. Where they conflict, users must be able to see which rule applies to the specific venue or event.

Clubs use the existing competition model. A club may create competitions, including member-only or open events, associated with one or more FishingVenues. Do not introduce a separate club-only competition type. Club championship points, seasonal leagues and multi-event standings are future capabilities, not MVP.

### Privacy

Club membership does **not** grant a club unrestricted ownership of a user's fishing history. A user's personal logbook remains their own data.

A club must not automatically gain access to:

- private notes
- private exact coordinates
- unrelated catches
- personal data not required for the club use case

Catches on club-managed waters may later contribute to privacy-preserving aggregate statistics. Advanced club statistics are not required for the initial MVP UI. The Catch and club-venue model must not prevent them.

Payment processing for membership fees, competition entry, venue bookings or day tickets is not part of the MVP. Design for later compatibility only. Do not introduce a payment provider because clubs exist.

Accounting, AGM management, committee minutes, elections, general document management, full CRM and advanced club financial reporting are outside the Club MVP.

---

## Web Application Hosting

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

IndexedDB holds unsynchronised Catch records, photographs and any captured location. Neon holds synchronised catch metadata, including location metadata. R2 holds photograph bytes.

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

## Diagnostic logging

FishingLogBook records **privacy-safe diagnostics** so offline failures (especially iOS Home Screen PWA IndexedDB hangs) can be investigated without a debugger attached.

### Client

The PWA writes diagnostic events to a **separate IndexedDB database** (`FishingLogBookDiagnostics`), not the Catch/photograph database. That isolation exists so diagnosing IndexedDB must not depend on the same Catch store succeeding, and so a logging failure cannot recurse into another Catch write.

Queued events are bounded (default 500). The oldest events are discarded first. Diagnostic persistence and upload must never:

- block Catch or photograph save
- block Catch/photograph synchronisation
- leave the Save button permanently busy

When connectivity returns, pending diagnostics upload **after** Catch/photograph synchronisation, in batches, to `POST /api/diagnostics/client`. Retry is bounded. Successfully uploaded events are removed. Client-generated event IDs provide lightweight duplicate protection.

Production persists Warning, Error and Critical. Information is retained for selected lifecycle events during Development. Debug is not normally persisted or uploaded from Production. Minimum persist level is configurable per environment.

Diagnostics must not include exact GPS coordinates, photographs or binary/base64, private catch notes, tokens, passwords, connection strings, secrets, Cognito tokens, or raw sensitive user information. Metadata is an explicit allow-list (operation name, elapsed milliseconds, store name, record count, retry number, HTTP status, platform/browser, online state, storage quota/usage).

A non-personal correlation ID is created for a user action such as Save Catch and sent as `X-Correlation-Id`. An anonymous session ID is a random GUID stored in localStorage.

### API

The API logs through structured `ILogger`. Request middleware records CorrelationId, RequestPath, HTTP method, StatusCode and ElapsedMilliseconds. Successful HTTP requests are Debug (not Information) so Production volume stays low. Request bodies and authentication headers are not logged.

Accepted client diagnostics are sanitised again on the server and written through `ILogger`. The API host uses **Serilog**; when `ExternalLogging` is configured it adds `Serilog.Sinks.Grafana.Loki`. Application and Domain must not reference Grafana or Serilog sink types. Credentials exist only on the server (user secrets / Fly secrets copied from Terraform outputs). Loki stream labels are `app` and `env` (`localhost`, `dev`, or `prod`). Do not use `ASPNETCORE_ENVIRONMENT` for `env` — Fly Dev also runs as Production. If Grafana is unconfigured or the sink fails, the API still runs.

Logging volume must stay within a free/low-cost tier during MVP. Do not log every UI click, every HTTP success, every Production IndexedDB read, renders, large objects, photos or catch payloads.

A Development-only inspector can show queued count, last diagnostic error, storage estimate and online/offline state. It is not shown in Production unless explicitly configured.

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

Do not provision mapping providers, analytics pipelines, payment providers or additional databases to satisfy Catch, location or club architecture. Those product concepts are designed in this document; they are not a reason to create cloud resources.

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

Repository database integration tests run in the same GitHub Actions `build-test`
workflow via Testcontainers PostgreSQL (`postgres:16-alpine` on `ubuntu-latest`).
They are automated CI tests, not a sandbox. They do **not** use Neon, a shared CI
database, or database connection secrets. Do not add a workflow database service
unless a later issue actually requires one.

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
- ICU data is fully loaded (`BlazorWebAssemblyLoadAllGlobalizationData`) so the PWA can set `en-GB` or `fr` at startup. Blazor WASM does not allow a dynamic culture change with the default reduced ICU dataset

Catch recording must still work offline, so translations are bundled with the PWA. User-generated content is not translated. Units (kg vs lb) are a separate user preference and must not be mixed into language resources.

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
Offline Catch + Photograph + optional location
    |
connectivity returns
    |
    v
Synchronisation
    |
    +----> Fly.io API (catch metadata + location metadata)
    |
    +----> R2 photograph upload
    |
    +----> Neon metadata
```

This must be tested on real Android and iPhone devices before substantial feature development proceeds (`docs/Requirements.md` # 33 and # 45; `BUILD.md` # 48 and # 49).

Location metadata is part of that offline Catch path. Architecture specifies that optional coordinates, accuracy, source, visibility and consent travel with the catch through IndexedDB and synchronisation, without blocking save when location is unavailable.

Mapping providers, heat maps, aggregated location analytics, payment providers and league scoring are not part of the first or second architecture milestone.

---

## Out of Scope for This Architecture Increment

This document describes Catch, location, photographs, synchronisation, FishingVenue and Club so later issues can implement them consistently.

It does **not** authorise, as part of issue #6:

- Application code, database migrations, or Terraform apply
- Mapping providers, public catch maps, or heat maps
- Payment processing
- Club championship / league scoring
- Creating additional cloud resources
