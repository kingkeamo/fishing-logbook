# Issue #237 investigation: validation evidence

Date: 2026-10-24. Base: `9083506717b3c4b02bccb149f7f204f265a2ddce`.
Branch: `feature/237-maui-hybrid-spike` (created before the three-option direction).
This is a documentation-only investigation. No new shared or mobile code exists,
so no artificial automated tests were added for prose. The ADR is proposed, not approved.

## Toolchain probe (before the additional stop instruction)

| Executed command | Observed result |
|---|---|
| `dotnet --info` | Windows x64; SDK 10.0.300 selected. SDKs 8.0.204 and 9.0.102 also installed; no global.json. Existing application projects target net10.0. |
| `dotnet workload list` | android 36.1.2/10.0.100; ios and maccatalyst 26.0.11017/10.0.100; maui-windows 10.0.0/10.0.100. **No maui-android**. No workload sets installed. |
| `dotnet new list maui` | No matching templates. |
| `Get-Command adb, java -ErrorAction SilentlyContinue` | Java resolves to Microsoft JDK 11; adb not on PATH. This does not establish that no Android SDK/device exists elsewhere. |
| `Get-ChildItem 'C:/Program Files/dotnet/packs' -Name` (filtered for Maui/Android) | Maui SDK and Android packs present, but insufficient for the MAUI Android target. |
| `dotnet build (Join-Path $probeDir 'Probe.csproj') --no-restore` | **Failed NETSDK1147: maui-android must be installed**, before compilation. |

The temporary probe was created outside the repository with these exact PowerShell
commands; it is not a mobile host or a deliverable:

```powershell
$probeDir = Join-Path $env:TEMP 'fishing-logbook-237-maui-probe'
New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
Set-Content -LiteralPath (Join-Path $probeDir 'Probe.csproj') -Value '<Project Sdk="Microsoft.NET.Sdk.Razor"><PropertyGroup><TargetFramework>net10.0-android</TargetFramework><UseMaui>true</UseMaui><SingleProject>true</SingleProject><OutputType>Exe</OutputType></PropertyGroup></Project>'
dotnet build (Join-Path $probeDir 'Probe.csproj') --no-restore
```

No workload installation, emulator launch, device connection, native compilation,
iOS build or signing was performed. A Windows iOS workload listing is not a working
Mac/Xcode toolchain. After the user's additional instruction, even toolchain repair
does not authorize implementing a host: architecture selection comes first.

## Initial baseline validation

All commands ran from repository root. Existing restored dependencies were reused.

| Executed command | Result |
|---|---|
| `dotnet build src/FishingLogBook.Web/FishingLogBook.Web.csproj --no-restore` | Passed, zero warnings/errors. |
| `dotnet build FishingLogBook.sln --no-restore` | Passed, zero warnings/errors. |
| `dotnet test FishingLogBook.sln --no-build --no-restore --logger "console;verbosity=quiet"` | Web 1900, Application 597, API 269, Shared 18, Migrations 10 passed. Infrastructure 39 passed/250 failed because sandbox denied Docker pipe access. |
| `dotnet test tests/FishingLogBook.Infrastructure.Tests/FishingLogBook.Infrastructure.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ItShouldCreateTheFinalCatchColumns --logger "console;verbosity=normal"` | Diagnostic rerun confirmed DockerUnavailableException caused by UnauthorizedAccessException on `npipe://./pipe/docker_engine`. |
| `dotnet test tests/FishingLogBook.Infrastructure.Tests/FishingLogBook.Infrastructure.Tests.csproj --no-build --no-restore --logger "console;verbosity=quiet"` outside sandbox | Passed all 289 with disposable PostgreSQL Testcontainers. |
| `dotnet format FishingLogBook.sln --verify-no-changes --no-restore` | Passed. Verify-only avoids unrelated source formatting in a documentation change. |
| `npm test` | 34 files / 405 tests passed. |
| `npm run test:browser` | First sandbox run produced test results but stalled before final exit; interrupted, not counted as passed. Rerun outside sandbox exited 0: **159 passed, 7 skipped** across Chromium/WebKit. |

Browser harness also runs `dotnet publish src/FishingLogBook.Web/FishingLogBook.Web.csproj -c Debug -o <repo>/artifacts/browser-tests/published-web --no-restore`.
Existing skips include WebKit offline navigation and conditional storage-quota behaviour;
do not count those as coverage. This harness validates real browser JS/IndexedDB/PWA
boundaries, not native hardware or the full authenticated mobile journey.

## Self review

- **Issue/AC:** investigation and updated ADR comparison covered. Original Android
  run/shared-MudBlazor AC not satisfied; superseded for this turn by explicit review
  gate. Keep issue open and PR draft pending architecture selection.
- **Architecture/CQRS:** PASS for investigation; existing server layering retained.
  ADR distinguishes native controls, new Razor UI and shared Razor UI explicitly.
- **Rules:** PASS for documentation scope; branch from fresh origin/main, no issue
  creation or deployment. Shared-Razor dependency change is conditional, not silently applied.
- **Security/privacy:** PASS for recommendation scope; identified single-client API
  validation, offline WebAuthn verification, account isolation, photo sanitisation,
  gallery privacy and key/backup loss as implementation gates, not solved features.
- **Database/migrations:** N/A changes; native SQLite/file transaction equivalence is
  specified for later real-store tests. No PWA or server migration is performed.
- **Tests/coverage:** existing regression evidence above; no new runtime code to test.
- **Browser:** existing harness exercised. Missing native sign-in → offline Catch →
  sync E2E could reveal callback, permission, crash durability and UI integration failures.
  Existing bUnit tests exercise current Web components/services with mocked boundaries;
  JS/Playwright exercises real IndexedDB and PWA shell. Neither substitutes for native
  E2E; native test infrastructure/device work is proposed after selection, without
  modifying production auth to force Playwright coverage.
- **Web/UI/localisation:** N/A changes; EN/FR and resources included in future AC.
- **Code smells:** no production changes; no abstractions introduced merely for reuse.
- **CI/deployment:** no changes, secrets, migrations, Terraform apply or deployment.

Findings discovered and fixes made:

1. Initial recommendation assumed shared Razor UI. Replaced with a three-option ADR
   and a native-control recommendation; marked RCL design conditional throughout.
2. Original backlog forced shared CatchCard/editor extraction. Updated it to follow
   the selected renderer and added an explicit owner-review gate.
3. Existing native auth risks include API single-client validation and browser PRF
   offline unlock, beyond generic token storage. Recorded both and acceptance tests.
4. A passing unit suite cannot prove native persistence or photo fidelity. Recorded
   crash/reconciliation tests and device gaps; no claims of native validation.

Remaining deliberate gaps: no approved architecture, native host, device evidence,
OIDC library selection, SQLite provider/encryption choice, gallery privacy decision,
minimum-OS/performance budgets, or Mac/signing setup. These are review/implementation
gates, not hidden completed work.

## Final validation

After the ADR/backlog review, all final commands exited 0:

| Executed command | Final result |
|---|---|
| `dotnet build FishingLogBook.sln --no-restore` | Passed, zero warnings/errors; includes Web WASM build. |
| `dotnet format FishingLogBook.sln --verify-no-changes --no-restore` | Passed; no formatting changes required. |
| `dotnet test FishingLogBook.sln --no-build --no-restore --logger "console;verbosity=quiet"` | **3083 passed**, none skipped: Web 1900, Application 597, Infrastructure 289, API 269, Shared 18, Migrations 10. Docker accessible for this run. |
| `npm test` | **405 passed** in 34 files. |
| `npm run test:browser` | **159 passed, 7 skipped**; includes harness Debug web publish. |
| `git diff --cached --check` | Passed. Relative Markdown document links also checked and resolve. |

The two final wording corrections distinguish a *proposed* backlog from an approved
one and make OS requirements conditional on the selected renderer. They affect prose
only. Test results do not validate any of the proposed mobile implementations.
