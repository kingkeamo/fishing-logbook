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

## Folder layout

```text
Pages/         → routable pages (each significant page in its own folder)
Components/    → reusable child components
Layouts/       → layouts (MainLayout)
Services/      → typed HTTP clients that call the API (I*Client implementations)
Localization/  → UiStrings.resx, culture service, MudLocalizer
Offline/       → offline / IndexedDB support (added in the offline milestone)
Models/        → UI-only models and enums
Configuration/ → strongly-typed config (e.g. ApiConfig)
wwwroot/       → index.html, manifest.webmanifest, service worker, appsettings*.json, css
```

## Page & component pattern (mandatory)

Each significant page/component uses the **partial-class** three-file pattern:

```text
PageName.razor        → @page, markup, minimal/no @code
PageName.razor.cs     → public partial class PageName : ComponentBase { ... }
PageName.razor.css    → component-scoped (isolated) CSS
```

- The code-behind is a **`partial class`** with the **same name** as the component
  (`public partial class SystemStatus`), not a separate `*Base` + `@inherits`.
- Put `[Inject]`, `[Parameter]`, lifecycle methods, and event handlers in `.razor.cs`.
- Do **not** place large code blocks inside `.razor` files.
- Scoped CSS (`PageName.razor.css`) **is** bundled by this project (Blazor CSS isolation);
  the bundle is referenced as `FishingLogBook.Web.styles.css` in `index.html`. Global
  styles live in `wwwroot/css/app.css`.

## Dependency injection

- Register client services, localization, and MudBlazor in `AddFishingLogBookWeb`
  (`ServiceCollectionExtensions.cs`). `Program.cs` only builds the host and calls that
  method.
- Use `[Inject]` on the code-behind for services (`I*Client`, `NavigationManager`,
  `IJSRuntime`, `IDialogService`, `ISnackbar`).
- Prefer `I*Client` interfaces from `Web/Services/` over concrete types.

## Client services (`Web/Services/`)

Pages and components call **typed client services**, not `HttpClient` directly.

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

## JavaScript interop

- Keep interop calls in the code-behind (not scattered in markup), typically in
  `OnAfterRenderAsync(firstRender)`.

## Before writing

Read at least **5 existing files of the same type** (page, component, client service)
before adding new UI. Match folder placement and the three-file naming pattern.
