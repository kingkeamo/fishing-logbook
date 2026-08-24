# FishingLogBook browser E2E

This project drives the real Blazor WebAssembly application, API, DbUp migrations,
IndexedDB and Cognito authorization flow in Chromium. It does not replace the smaller
Playwright storage/service-worker harness under `src/FishingLogBook.Web/BrowserTests`.

## Windows prerequisites

- .NET 10 SDK
- Docker Desktop running Linux containers
- trusted local .NET development certificate (`dotnet dev-certs https --trust`)
- a dedicated DEV Cognito E2E user with no MFA/CAPTCHA requirement; the setup completes
  onboarding through the real UI in each disposable database
- `http://localhost:5019/authentication/login-callback` configured for the DEV Cognito client

Set credentials only in the current shell or an approved secret store:

```powershell
$env:E2E_COGNITO_USERNAME = "<dedicated E2E user>"
$env:E2E_COGNITO_PASSWORD = "<secret>"
npm --prefix tests/FishingLogBook.E2E ci
npm --prefix tests/FishingLogBook.E2E run install:browsers
npm --prefix tests/FishingLogBook.E2E test
```

From the repository root, use the convenience scripts:

```powershell
npm run test-e2e
npm run test-e2e-debug
npm run test-e2e-single -- "records and edits locally offline"
```

`test-e2e` runs the complete suite in visible Chromium with one worker.
`test-e2e-debug` opens the Playwright Inspector and also makes the authentication
setup browser visible. `test-e2e-single` uses Playwright's title matching, so supply a
distinctive full or partial test title after `--`.

The same commands are also available directly inside `tests/FishingLogBook.E2E`.

The default command starts a disposable PostgreSQL 18 container on port `55433`, applies
the real migrations, and starts the API and Web projects. Override the port with
`E2E_POSTGRES_PORT`. To use an already-running stack, set `E2E_EXTERNAL_STACK=true` and
`E2E_BASE_URL`. A separate global teardown removes the run-owned container even when
Playwright forcibly terminates its WebServer process.

Each run stores browser authentication state under `.auth/` and artifacts under
`artifacts/`; both are ignored. Authentication setup never records traces, screenshots or
video. Local runs retain failure traces for developer diagnosis. CI disables authenticated
traces because network metadata can contain bearer tokens and uploads screenshots/report
only. Never upload `.auth` or log credentials/tokens. The disposable database is removed
after the run, so no shared DEV/private-alpha Catch data is read, changed or deleted.

## Coverage boundary

The suite proves an authenticated online journey and an online-to-offline-to-reconnect
journey in one live Chromium context. It does not prove installed mobile PWA lifecycle,
offline cold restart (#132), iOS Safari/WebKit, biometrics/WebAuthn, Samsung OEM behavior,
or real camera behavior. Those remain emulator/physical-device work described in #133.

## GitHub Actions

The `browser-e2e` workflow runs on pull requests, nightly, and manually after the
repository variable `E2E_ENABLED=true` and repository secrets
`E2E_COGNITO_USERNAME` / `E2E_COGNITO_PASSWORD` are configured. The setup captures the
standard Blazor OIDC session storage into an ignored, permission-restricted ephemeral file
and restores it into each test context; this mirrors the current application session rather
than bypassing authorization. Until configured the job is
visibly skipped rather than attempting an insecure fallback. Only failure diagnostics are
uploaded; `.auth` is never included.
