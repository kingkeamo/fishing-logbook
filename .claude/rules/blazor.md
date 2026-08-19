---
paths:
  - "src/FishingLogBook.Web/**/*.cs"
  - "src/FishingLogBook.Web/**/*.razor"
  - "src/FishingLogBook.Web/**/*.razor.css"
---

# Blazor Web Conventions (`FishingLogBook.Web`)

Server-side C# (Api, Application, Infrastructure) is in **`csharp.md`**.
Blazor tests are in **`testing-blazor.md`**.

## Hosting & project

- **Blazor WebAssembly** Progressive Web App (`Microsoft.NET.Sdk.BlazorWebAssembly`),
  deployed as static assets (Cloudflare Pages) — it is not hosted by the API.
- UI: **MudBlazor**. Register MudBlazor (and all other Web services) via
  `AddFishingLogBookWeb` and include the MudBlazor CSS/JS in `wwwroot/index.html`.
  Use MudBlazor components; do not add Bootstrap or other UI frameworks.
- The PWA is mobile-first with a responsive desktop layout, and supports light and dark
  themes via `MudThemeProvider`.
- Shared contracts come from `FishingLogBook.Shared`.

**Do not** reference `FishingLogBook.Infrastructure` or `FishingLogBook.Application` from
Web — call the API via client services only.

**Comments:** Same as **`csharp.md` → Comments** — no explanatory comments on production
code; tests use Arrange / Act / Assert only.

## Feature-first folder layout (mandatory)

`FishingLogBook.Web` is **feature-first**. If a class exists because of one product
feature, it must live under that feature.

```text
Features/<Feature>/Pages/                    → routable pages for that feature
Features/<Feature>/Components/               → feature-owned reusable UI
Features/<Feature>/Models/                   → feature-owned UI/domain view models
Features/<Feature>/Services/                 → feature-owned *Service only
Features/<Feature>/Clients/                  → feature-owned *Client (API clients)
Features/<Feature>/Providers/                → feature-owned *Provider
Features/<Feature>/Http/                     → DelegatingHandlers and other HTTP pipeline types
Features/<Feature>/Offline/Stores/           → feature-owned *Store (IndexedDB persistence)
Features/<Feature>/Offline/Synchronisers/    → feature-owned *Synchroniser
Features/<Feature>/Storage/Stores/           → *Store persistence that is not Catch offline
```

**Role decides the folder, not just the name.** A class whose role suffix and folder
disagree is misplaced — move it, do not rename it to fit. Create a role folder only when
the feature actually has that role; do not create empty directories.

| Suffix | Folder |
|---|---|
| `*Service` / `I*Service` | `Services/` |
| `*Client` / `I*Client` | `Clients/` |
| `*Provider` / `I*Provider` | `Providers/` |
| `*Store` / `I*Store` | `Offline/Stores/` (or `Storage/Stores/`) |
| `*Synchroniser` / `I*Synchroniser` | `Offline/Synchronisers/` |
| `*Factory` / `I*Factory` | `Factories/` |
| `DelegatingHandler` subclasses | `Http/` |
| `*Model` | `Models/` |
| `*Config` / `*Options` | root `Configuration/` |

Additional rules:

- **Namespaces must match the folder path.** No exceptions.
- **One production type per file.**
- Do not disguise a role: an HTTP message handler is not a service, and an IndexedDB
  cache is a `*Store`.
- Do not rename a class merely to earn a suffix. Classify its actual architectural role
  first; if none of the roles above fit, leave the name and say why.
- Do not create `Helpers/`, `Utils/`, `Managers/`, `Common Services/` or any other
  dumping ground.
- A type used by more than one unrelated feature is not feature-owned — move it to the
  root `Common/` (for example `Common/Offline/OfflineOperation.cs`).
- Razor pages, components and layouts keep their natural names and the three-file
  convention. Do **not** give them role suffixes.

**Do not** create global dumping-ground folders for feature-specific `Models`,
`Services`, `Offline`, or `Components`.

Root-level folders are only for genuinely cross-feature Web infrastructure:

```text
Components/      → shared UI used by multiple unrelated features (e.g. LanguageSwitcher)
Browser/         → browser APIs (Location, Network, etc.)
Layouts/         → app layouts (MainLayout)
Pages/           → cross-cutting pages only (e.g. NotFound)
Common/          → types used by multiple unrelated features (e.g. SyncStatus)
Localization/    → UiStrings.resx, culture service, MudLocalizer
Configuration/   → strongly-typed config (ApiConfig, DiagnosticsClientConfig)
wwwroot/         → index.html, manifest, service worker, appsettings, css, JS
```

JavaScript under `wwwroot/js/` stays as structured by the JavaScript refactor. Do not
move `package.json`, `package-lock.json`, or `node_modules`.

### Canonical feature example

When adding Catch (or any new feature), follow this shape. Do not create empty folders
that the feature does not need.

```text
Features/
    Catch/
        Pages/
            CatchLog/
                CatchLog.razor
                CatchLog.razor.cs
                CatchLog.razor.css

        Components/
            CatchCard/
                CatchCard.razor
                CatchCard.razor.cs
                CatchCard.razor.css

        Models/
            CatchModel.cs
            CatchLocationModel.cs

        Services/
            ICatchClient.cs
            CatchClient.cs

        Offline/
            ICatchStore.cs
            CatchStore.cs
            ICatchSynchroniser.cs
            CatchSynchroniser.cs
```

Namespaces follow folders:

```text
FishingLogBook.Web.Features.Catch.Models
FishingLogBook.Web.Features.Catch.Services
FishingLogBook.Web.Features.Catch.Offline
FishingLogBook.Web.Features.Diagnostics.Services
```

### Before adding a new file

1. Which feature owns it? If one feature, it goes in `Features/<Feature>/`.
2. What architectural role does it have? That decides the subfolder and the type name.
3. Only then create the file.

Shared API DTOs stay in `FishingLogBook.Shared`. Do not move them into Web to satisfy
this layout.

## Page & component pattern (mandatory)

Every significant Razor page, reusable component, and layout **owns a directory**.
Do not leave `.razor` files loose directly inside `Pages`, `Components`, or `Layouts`.

Each uses the **partial-class** three-file pattern:

```text
PageName.razor        → @page, markup, minimal/no @code
PageName.razor.cs     → public partial class PageName : ComponentBase { ... }
PageName.razor.css    → component-scoped (isolated) CSS
```

```text
Components/LanguageSwitcher/
    LanguageSwitcher.razor
    LanguageSwitcher.razor.cs
    LanguageSwitcher.razor.css

Layouts/MainLayout/
    MainLayout.razor
    MainLayout.razor.cs
    MainLayout.razor.css
```

- The code-behind is a **`partial class`** with the **same name** as the component
  (`public partial class SystemStatus`), not a separate `*Base` + `@inherits`.
- Put `[Inject]`, `[Parameter]`, lifecycle methods, and event handlers in `.razor.cs`.
- Do **not** place large code blocks inside `.razor` files.
- Do **not** append `Model` / `Service` / role suffixes to Razor names. Use natural UI
  names (`CatchLog`, `CatchCard`, `LanguageSwitcher`, `MainLayout`).
- New components include `.razor.css` even when empty, so the three-file shape stays
  predictable. Do not add an empty `.razor.css` to an existing component in a behavioural
  refactor unless styling is actually required (CSS isolation can change rendering).
- Scoped CSS **is** bundled by this project (Blazor CSS isolation); the bundle is
  referenced as `FishingLogBook.Web.styles.css` in `index.html`. Global styles live in
  `wwwroot/css/app.css`.

## Production C# role suffixes (mandatory)

Class names must make the architectural role obvious in search, stack traces, DI, and
directory listings.

| Role | Suffix | Example |
|---|---|---|
| UI/domain view model | `Model` | `CatchLocationModel` |
| Service | `Service` | `LocationService` |
| API client | `Client` | `CatchClient` |
| Persistence/store | `Store` | `CatchStore` |
| Synchronisation/orchestrator | `Synchroniser` | `CatchSynchroniser` |
| Configuration | `Options` or `Config` | `ApiConfig`, `DiagnosticsClientConfig` |
| Request | `Request` | |
| Response | `Response` | |
| DTO | `Dto` | Only actual transport contracts (usually Shared) |
| Validator | `Validator` | |
| Mapper | `Mapper` | |
| Provider | `Provider` | |
| Factory | `Factory` | |
| Repository | `Repository` | |

Do **not** add meaningless suffixes such as `Class`, `Object`, `Helper`, or `Manager`
unless `Manager` is a genuine established role (normally avoid it).

Interfaces use the `I` prefix **and** the role suffix:

```text
ICatchService
ICatchClient
ICatchStore
ICatchSynchroniser
ILocationService
```

Do **not** name them `CatchServiceInterface`, `ICatch`, or `ITestCatch` when the role
would otherwise be unclear.

Do **not** rename Shared API DTOs merely because they do not end in `Model`.

## Dependency injection

- Register client services, localization, and MudBlazor in `AddFishingLogBookWeb`
  (`ServiceCollectionExtensions.cs`). `Program.cs` only builds the host and calls that
  method.
- Use `[Inject]` on the code-behind for services (`I*Client`, `NavigationManager`,
  `IJSRuntime`, `IDialogService`, `ISnackbar`).
- Prefer feature-owned `I*Client` / `I*Service` interfaces over concrete types.
- Register new Web services in `AddFishingLogBookWeb`, not only in `Program.cs`.

## Client services

Pages and components call **typed client services**, not `HttpClient` directly.
Feature-owned clients live in `Features/<Feature>/Services/`. Cross-feature browser
abstractions live in `Browser/`.

- Constructor-inject `HttpClient` (its `BaseAddress` is configured from `Api:BaseUrl`).
- Methods call the API routes (e.g. `health`, `api/system/database`) using
  `GetFromJsonAsync` / `GetAsync` + `ReadFromJsonAsync`.
- Return `Shared` contract types (DTOs). Handle non-success status codes explicitly — the
  API returns error status codes (e.g. 503) rather than faking success, so clients and
  pages must reflect real state.
- Do not hard-code API URLs; they come from `wwwroot/appsettings*.json` (`Api:BaseUrl`).
  Remember all Blazor WASM configuration sent to the browser is public — never put secrets
  in `appsettings*.json`, JS, or Blazor assemblies.

## Markup & UI conventions

- Use MudBlazor components (`MudContainer`, `MudStack`, `MudPaper`, `MudText`, `MudChip`,
  `MudButton`, `MudProgressCircular`, etc.). Follow neighbouring pages.
- Design mobile-first; verify light and dark mode.
- **Localisation (mandatory):** user-visible copy uses `IStringLocalizer<UiStrings>`
  (`@Loc["Key"]`). Add the key to `Localization/UiStrings.resx` (en-GB) and
  `Localization/UiStrings.fr.resx`. Do not hard-code English in `.razor` files. Keep
  test selectors on stable `id` values, never on translated text.
- **Testability:** add stable `id="..."` on primary panels, rows, and buttons
  (e.g. `id="refresh-status-button"`, `id="status-row-database"`).
- Avoid inline `style="..."` on HTML elements; prefer scoped `.razor.css` or `app.css`.
  MudBlazor component parameters such as `Class`/`Style` are component API and may be used
  where appropriate.

## Application shell (mandatory)

`MainLayout` is the single owner of the authenticated shell: AppBar, navigation drawer,
and the global content gutters. Pages must not decide whether navigation is expanded,
whether a hamburger shows, how wide the outer gutters are, how the AppBar is laid out, or
how the signed-in user is presented.

```text
MudLayout
├── MudAppBar        → menu button, brand, spacer, language, theme, user menu
├── MudDrawer        → responsive navigation
└── MudMainContent
    └── .app-shell-content   → global gutters
        └── AppErrorBoundary → @Body
```

- Use MudBlazor's own responsive primitives. **Do not** add a bespoke
  `window.innerWidth`/resize JS service for layout the framework already solves.
- The drawer uses `DrawerVariant.Responsive` with `Breakpoint.Md`: collapsed and
  hamburger-driven below it, persistent at and above it.
- Hide chrome that is only needed at one size with MudBlazor's display utilities
  (`d-md-none`, `d-none d-sm-flex`) rather than conditional rendering, so the contract
  stays assertable in bUnit.
- The AppErrorBoundary wraps `@Body` **only**. Never wrap the AppBar or drawer in it — a
  page failure must leave navigation usable.
- Pages own their *content* max-width (`MudContainer MaxWidth="…" Gutters="false"`) and
  nothing else. A focused workflow stays narrow; a list screen may use more width.
- The AppBar must never block on a network call. Load user/profile enrichment in
  `OnAfterRenderAsync` and render a safe default first.

## JavaScript interop

- Keep interop calls in the code-behind (not scattered in markup), typically in
  `OnAfterRenderAsync(firstRender)`.

## Before writing

1. Which feature owns this file? If one feature, it belongs in `Features/<Feature>/`.
2. What architectural role does it have? That decides the subfolder and the type name.
3. Read at least **5 existing files of the same type** (page, component, client service)
   before adding new UI. Match feature folder placement and the three-file naming pattern.
