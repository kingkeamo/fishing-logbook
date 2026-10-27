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
- callback: `com.fishinglogbook.mobile.demo://oauth2redirect`
- logout callback: `com.fishinglogbook.mobile.demo://signedout`

Configuration validation rejects non-HTTPS authorities and API endpoints, empty or
malformed client IDs, foreign callback schemes and API scopes belonging to another
resource.

## Browser callback and storage

Authorization uses the system browser. The pending state and PKCE verifier are encrypted
with MAUI `SecureStorage` before the browser opens so Android can finish a callback after a
cold application start. A pending transaction:

- expires after five minutes;
- is removed before callback validation and can therefore be consumed only once;
- is removed when sign-in is cancelled or fails to launch;
- is rejected after expiry, on a callback URI mismatch or on a state mismatch.

The Android activity handles both cold launch and `SingleTop` delivery. iOS registers the
same custom scheme and forwards `OpenUrl` callbacks. Android runtime behaviour must be
verified on an emulator or device. iOS is compile/configuration-only until the project is
built and exercised on a Mac and physical device.

Both platforms forward only the registered sign-in and sign-out callbacks. Forwarding never
throws back into the platform entry point: Android's `async void` intent dispatch and the
iOS fire-and-forget `OpenUrl` path contain every failure and log only the exception type,
never the callback URI, authorization code, state or exception message.

Custom schemes can be claimed by another installed application. PKCE and state validation
limit interception risk, but verified Android App Links and iOS Universal Links are
preferred before production. They require an owned HTTPS domain, platform association
files and reviewed Cognito callback changes, so they are not introduced by #242.

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
