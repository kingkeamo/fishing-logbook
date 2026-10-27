# Native mobile authentication

Issue #242 adds native Cognito sign-in to the .NET MAUI application. It does not change
the Blazor PWA authentication or Cognito infrastructure.

## OIDC client

The mobile application uses `Duende.IdentityModel.OidcClient` 7.1.1. The package is a
maintained, certified OpenID Connect relying-party implementation for native applications,
implements RFC 8252, targets .NET 8 and .NET Standard 2.0, and is compatible with the
application's .NET 10 MAUI targets. It is licensed under Apache-2.0.

The package owns discovery, issuer and signature validation, nonce and state validation,
authorization-code exchange, PKCE S256 and refresh processing. MAUI browser APIs are only
used to launch the system browser and deliver the callback; they are not treated as an
OIDC implementation. Pushed Authorization Requests are disabled because Cognito does not
support them.

Cognito publishes its authorize, token, revocation and logout endpoints on the hosted-UI
domain rather than the issuer host. Discovery endpoint validation stays enabled and allows
only the configured `HostedUiDomain` as an additional endpoint base. Endpoints on any other
host are rejected before the browser opens.

The package requires `Microsoft.Extensions.Logging.Abstractions` 10.0.13 or later. The
mobile project overrides that single package with `VersionOverride`; the solution-wide pin
and the API, Web and server projects stay on 10.0.11.

OIDC client logging is disabled. Its Debug and Trace levels can contain authorization
responses or personally identifiable information. Debug builds on Android write application
warnings and HTTP request lines to logcat under the `FishingLogBook` tag; release builds do
not register that provider. Authentication messages never include token values.

## Configuration

Development configuration is embedded from
`Configuration/mobileauth.Development.json`. Select a different environment at build time
with `MobileEnvironment`; that environment must provide its own reviewed configuration
file. Cognito client IDs and endpoint URLs are public configuration. Client secrets and
tokens must never be added to these files.

The current development values are:

- authority: `https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_J4Kt9QB06`
- native client: `513lhdbas693dqq2bs58v1ri8h`
- API: `https://fishing-logbook-dev-api.fly.dev`
- callback: `https://fishing-logbook-dev.pages.dev/native/oauth2redirect`
- logout callback: `https://fishing-logbook-dev.pages.dev/native/signedout`
- iOS < 17.4 only: `com.fishinglogbook.mobile.demo://oauth2redirect` and
  `com.fishinglogbook.mobile.demo://signedout`

Configuration validation rejects non-HTTPS authorities and API endpoints, empty or
malformed client IDs, HTTPS callbacks with a non-default port, user info, query, fragment
or a path other than `/native/oauth2redirect` and `/native/signedout`, sign-in and sign-out
callbacks on different hosts, foreign callback schemes and API scopes belonging to another
resource.

## Browser callback and storage

Authorization never uses an embedded web view. The pending state and PKCE verifier are encrypted
with MAUI `SecureStorage` before the browser opens so Android can finish a callback after a
cold application start. A pending transaction:

- expires after five minutes;
- is removed before callback validation and can therefore be consumed only once;
- is removed when sign-in is cancelled or fails to launch;
- is rejected after expiry, on a callback URI mismatch or on a state mismatch.

## Verified App Links and Universal Links

Cognito accepts HTTPS callback and sign-out URLs for the native public client with
Authorization Code + PKCE; `logout_uri` must exactly match an allowed sign-out URL. The Dev
native client registers both the HTTPS URLs and, for iOS < 17.4 only, the private-use
scheme URLs. Production configuration is unchanged.

Android:

- `MainActivity` declares one `autoVerify` intent filter for
  `https://fishing-logbook-dev.pages.dev` limited to `/native/oauth2redirect` and
  `/native/signedout`. The private-use scheme is no longer registered on Android.
- Sign-in and sign-out open an Auth Tab (androidx.browser 1.9) when the default browser
  supports it. Auth Tab verifies the Digital Asset Links for the redirect host and path and
  returns the result to the launcher registered in `MainActivity.OnCreate`, including after
  process recreation. A cancelled, failed-verification or timed-out Auth Tab clears the
  pending sign-in and reports `Cancelled` to the page.
- Browsers without Auth Tab use a Custom Tab. Chrome hands the HTTPS redirect to the
  verified App Link because the tab was launched by the app; the activity receives it via
  cold launch or `OnNewIntent`. A Custom Tab closed by the user is not reported, so the
  page keeps its Cancel action.
- `assetlinks.json` binds `com.fishinglogbook.mobile.demo` to the SHA-256 fingerprints in
  `src/FishingLogBook.Web/build/app-links.dev.json`. The committed fingerprint is the local
  Android debug keystore used for emulator validation; debug keystores are per machine, so
  other developers' or CI-built debug APKs will not verify until their fingerprints are
  added. Unverified installs never receive the callback: there is no unverified fallback.

iOS:

- `Entitlements.plist` declares `applinks:` and `webcredentials:` for the Dev host.
- iOS 17.4+ uses `ASWebAuthenticationSession` with an HTTPS callback for the exact host and
  path. iOS < 17.4 cannot claim HTTPS callbacks in an authentication session, so
  `MauiProgram` selects the documented private-use scheme callbacks there.
- `apple-app-site-association` is generated only when the GitHub `dev` variable
  `APPLE_TEAM_ID` is a valid 10-character Team ID; an invalid value fails the deployment and
  an absent value skips the file. Until a paid Apple team is configured, iOS 17.4+ HTTPS
  callbacks cannot complete. iOS code is compiled on Windows only; it has not been linked,
  signed or run.

Association files are generated by `src/FishingLogBook.Web/build/configure-app-links.mjs`
during the Dev Pages deployment only. It refuses any other environment, validates the
package, fingerprints, paths and Team ID, serves the files as `application/json`, and
writes no-script `/native/*` pages with `no-store` and `no-referrer` for the case where a
browser displays the callback instead of handing it to the app. The production Web
artifact check still rejects any Dev host marker.

HTTPS callback matching compares scheme, host, port and path exactly.

Both platforms forward only the registered sign-in and sign-out callbacks. Forwarding never
throws back into the platform entry point: Android's `async void` intent dispatch and the
iOS fire-and-forget `OpenUrl` path contain every failure and log only the exception type,
never the callback URI, authorization code, state or exception message.

The remaining iOS < 17.4 custom scheme can be claimed by another installed application.
PKCE and state validation limit interception risk on that legacy path. Production must use
its own verified domain, release signing fingerprints and Team ID before its native client
is enabled.

Refresh credentials and the account binding are stored in `SecureStorage`. Access tokens
remain in memory. Tokens are not stored in SQLite, preferences, URLs, logs or WebView
storage.

## Refresh and account isolation

Access-token refresh is serialized. Concurrent callers wait for one refresh operation and
receive the same renewed session. A rotated refresh token is persisted before the renewed
access token is exposed.

Refresh failures are classified from the Cognito token-endpoint error:

- `invalid_grant` and the non-retryable OAuth errors `invalid_client`, `invalid_request`,
  `invalid_scope`, `unauthorized_client` and `unsupported_grant_type` are permanent. They
  clear only authentication state and the stored refresh credential, so no further refresh
  is attempted.
- Every other failure, including network errors, HTTP failures without a protocol error,
  `temporarily_unavailable`, discovery failures and an owner check that cannot complete, is
  retryable. The credential is retained, and refresh is suspended for an exponential backoff
  of 15 seconds doubling to a 5-minute maximum. A still-valid access token keeps working
  during the backoff; an expired one fails immediately without contacting Cognito.

Unclassifiable failures such as identity-token validation errors are therefore retried at
most once per backoff window rather than signing the user out, which keeps an offline or
degraded device signed in without allowing a tight retry loop.

Every authenticated session has an owner and generation lease. An owner transition:

1. marks the old generation stale and cancels its token;
2. waits for an in-progress guarded commit;
3. activates a new generation only after the old commit boundary has completed.

Each transition request takes a sequence number. When transitions overlap, only the most
recently requested one is applied; earlier requests waiting on the commit boundary are
discarded, so a stale login, logout or refresh cannot overwrite the final owner or
generation. A transition to the owner that is already active and current is a no-op, so a
same-account token refresh does not cancel that account's in-flight work.

Future storage and synchronization operations must perform their final local commit
through this lease. A stale lease cannot commit after a completed owner transition.
Authentication and secure-storage failures do not call local catch deletion.

## HTTP and logout

The authorized HTTP client accepts only the exact configured HTTPS API origin and the
`api/` path namespace beneath the configured base path. It rejects other hosts, HTTP
downgrades, non-API paths such as `/health`, and raw or percent-encoded dot segments and
backslashes before obtaining a token. Automatic redirects are disabled
on all authentication-related clients so a bearer header cannot be forwarded to another
origin. Presigned object-storage URLs use the separate anonymous client.

Local sign-out and owner invalidation happen before Cognito cleanup. Refresh-token
revocation and managed-browser logout are best effort. Either may fail without restoring
the local session or deleting pending offline catches.

## Android validation

Use the installed Android SDK and an emulator with a browser:

```powershell
dotnet build src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj `
  -f net10.0-android `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk"
```

Validate these journeys without recording tokens, authorization codes or user PII:

1. Fresh install opens the localized sign-in page.
2. Sign-in opens Cognito in the system browser and returns to the app.
3. Cancelling removes the pending transaction and permits a new sign-in.
4. Start sign-in, stop the app process, then complete sign-in to verify a cold callback.
5. Call the protected current-user endpoint.
6. Force stop and reopen to verify refresh-based restoration.
7. Trigger concurrent protected requests near expiry and verify one refresh.
8. Sign out with the network available and unavailable.
9. Sign in as a different account and verify the former session cannot publish a response
   or guarded synchronization commit.

The sign-in page subscribes to callback completion while visible, so a failed or rejected
callback replaces the waiting message with an error instead of leaving the page pending.

Android's own `ActivityTaskManager` log records the authorize URL, including `state` and
the PKCE challenge, when the browser intent starts. That is platform behaviour outside the
application. It does not include the code verifier, authorization code or tokens.

Do not use Terraform apply, deploy the API, publish the application or change live Cognito
configuration as part of this validation.
