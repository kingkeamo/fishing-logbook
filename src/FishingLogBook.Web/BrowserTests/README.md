# Playwright browser tests

This suite is a **second-level regression layer** for Chromium and WebKit. It is not a
substitute for testing on a real iPhone, including Home Screen PWA, Safari, and iOS
IndexedDB behaviour.

JavaScript unit tests live beside their modules in `wwwroot/js/**/*.test.js` and are
excluded from publish.

## What it covers

- Catch IndexedDB write/read
- Close/reload/read of Catch records
- Photograph persistence
- Diagnostic database isolation from the Catch database
- A simplified service-worker application shell (Chromium also checks offline navigation)

Playwright WebKit cannot navigate while `context.setOffline(true)` is set. That offline
reload is therefore Chromium-only here. It is still not a substitute for a real iPhone,
Home Screen PWA, or iOS IndexedDB.

## What it does not cover

- Real iPhone / iOS WebKit
- Home Screen PWA install
- Playwright WebKit offline navigation (`setOffline` causes an internal WebKit error)
- The production `service-worker.published.js` asset manifest and Cloudflare redirect handling
  (those remain covered by .NET source tests and real-device checks)
- Authenticated Blazor WASM / Profile UI. Production authentication is Cognito/OIDC.
  A deterministic authenticated Playwright host needs architecture beyond a product
  feature ticket and must not contaminate production Web auth. Profile confidence
  comes from bUnit, API, Testcontainers, and ProfileClient tests.

## Commands

From the repository root:

```bash
npm run test:browser
npm run test-local-browser
```

Install browsers once with `npx playwright install chromium webkit`.
