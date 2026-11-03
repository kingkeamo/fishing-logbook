# TestFlight internal testing for Catch

This guide prepares the first **internal beta** of Catch on a real iPhone. It is not a
production or public App Store release. Internal builds always embed
`MobileEnvironment=Development`, the Development API
(`https://fishing-logbook-dev-api.fly.dev`) and the existing Development Cognito user
pool.

The normal workflow builds, signs and uploads an IPA to App Store Connect. A signed IPA
remains available as a GitHub artifact, and the same workflow can still be run in
artifact-only mode.

Do **not** paste private keys, certificate passwords or API secrets into a pull request
or chat. Create them in Apple portals and GitHub Secrets only.

## Identity

| Setting | Value |
|---|---|
| App Store Connect app | Catch, But Don't Forget |
| Bundle identifier (Release / TestFlight) | `com.catchbutdontforget.app` |
| Debug / simulator bundle identifier | `com.fishinglogbook.mobile.demo` |
| Apple Team ID | `LHP9K4946H` |
| App Store SKU | `cbdf-ios-001` |
| App Store version | `1.0` (Prepare for Submission) |
| On-device name | `Catch` |
| Internal TestFlight group | `CatchButDontForget` |
| Development API | `https://fishing-logbook-dev-api.fly.dev` |

Do not recreate the App Store Connect app, bundle identifier or internal testing group.
They already exist. There are currently no TestFlight builds.

Debug and simulator installs keep `com.fishinglogbook.mobile.demo` so a TestFlight build
does not overwrite local development.

`CFBundleShortVersionString` for Release is `1.0` so the IPA attaches to the existing
App Store version. `CFBundleVersion` is the unique integer build number selected from
App Store Connect. That build number is not the App Store listing version.

## What the repository already does

- iOS Release builds use `com.catchbutdontforget.app` and display version `1.0`.
- Unsigned simulator CI is the `ios` job in `.github/workflows/mobile-build-test.yml`.
  It compiles `net10.0-ios` for `iossimulator-arm64` without certificates. It does not
  prove a real-device install.
- The signed IPA workflow is `.github/workflows/mobile-testflight.yml`. It runs only
  when started by hand from `main`, always embeds `MobileEnvironment=Development`,
  preserves the IPA as a GitHub Actions artifact, and can upload only to TestFlight
  internal testing. It does not apply Terraform, submit the app for review or release
  it publicly.
- Native HTTPS callbacks stay:
  - `https://fishing-logbook-dev.pages.dev/native/oauth2redirect`
  - `https://fishing-logbook-dev.pages.dev/native/signedout`
- iOS < 17.4 still uses the existing private-use scheme
  `com.fishinglogbook.mobile.demo://…`. Cognito Terraform is unchanged.
- `apple-app-site-association` is generated for both
  `LHP9K4946H.com.fishinglogbook.mobile.demo` and
  `LHP9K4946H.com.catchbutdontforget.app` when the GitHub `dev` variable
  `APPLE_TEAM_ID` is `LHP9K4946H`.

No Cognito Terraform change is required for the Release bundle ID. Those HTTPS URLs
are already registered on the Development native client.

## Auth callback verification

| Path | iOS 17.4+ TestFlight | iOS < 17.4 | Debug simulator |
|---|---|---|---|
| HTTPS Universal Link | Requires AASA for `com.catchbutdontforget.app` | Not supported by `ASWebAuthenticationSession` | Uses demo bundle AASA |
| Custom scheme | Not used | Existing demo scheme, still registered in `Info.plist` | Existing demo scheme |

Until `APPLE_TEAM_ID` is set and Dev Pages is redeployed, a TestFlight install cannot
complete verified HTTPS sign-in. That is an Apple/GitHub configuration step, not a
Cognito Terraform change.

## What you must do in Apple portals

Complete these on a trusted machine. The repository does not contain certificates,
provisioning profiles or App Store Connect API keys.

### 1. Confirm the existing App Store Connect app

1. Open [App Store Connect](https://appstoreconnect.apple.com).
2. Confirm **Catch, But Don't Forget**, bundle ID `com.catchbutdontforget.app`,
   Team ID `LHP9K4946H`, version **1.0** in **Prepare for Submission**.
3. Confirm internal group **CatchButDontForget**. Do not create another app or group.

### 2. Create an Apple Distribution certificate

1. Open [Apple Developer](https://developer.apple.com/account/resources/certificates/list)
   for team `LHP9K4946H`.
2. Create a certificate of type **Apple Distribution**.
3. Create the certificate signing request on a machine you control, download the
   `.cer`, and export **Apple Distribution** plus its private key as a PKCS#12
   (`.p12`) with a strong password.
4. Store the `.p12` and password in a password manager and an offline encrypted
   backup. Losing this private key requires a new certificate and a new profile.

Do not commit the `.p12`, a base64 copy, or the password.

### 3. Create an App Store provisioning profile

1. In Certificates, Identifiers & Profiles, open **Profiles**.
2. Create an **App Store** distribution profile (not Development, Ad Hoc or
   Developer ID).
3. App ID: **explicit** `com.catchbutdontforget.app`.
4. Certificate: the Apple Distribution certificate from the previous step.
5. Enable **Associated Domains** on the App ID if it is not already enabled. The
   entitlement `applinks:fishing-logbook-dev.pages.dev` is required for HTTPS
   sign-in.
6. Download the `.mobileprovision`. Do not commit it.

### 4. Create an App Store Connect API key

1. App Store Connect → **Users and Access** → **Integrations** → **App Store Connect API**.
2. Create a key named `catch-testflight-internal`.
3. Access: **App Manager**. That role can upload builds, read build processing state
   and assign the existing internal group. Do not use Account Holder. Developer is
   not enough to assign a beta group.
4. Limit the key to the Catch, But Don't Forget app if App Store Connect offers
   per-app restriction.
5. Download the `.p8` once. Apple will not show it again.
6. Record the **Issuer ID** and **Key ID**. Store the `.p8` in a password manager.

The `.p8` is a private key. Never commit it, print it in logs, or paste it into a
pull request.

### 5. Configure GitHub Actions signing secrets

Repository **Settings** → **Secrets and variables** → **Actions**. These three are
**repository** secrets so artifact-only runs can sign without the publishing
environment:

| Secret | Format |
|---|---|
| `IOS_DISTRIBUTION_CERTIFICATE_P12_BASE64` | Base64 of the `.p12` file, no line breaks |
| `IOS_DISTRIBUTION_CERTIFICATE_PASSWORD` | PKCS#12 password, plain text |
| `IOS_PROVISIONING_PROFILE_BASE64` | Base64 of the `.mobileprovision` file, no line breaks |

Encode on Windows:

```powershell
[Convert]::ToBase64String(
  [IO.File]::ReadAllBytes("$env:USERPROFILE\secure\catch-distribution.p12")
) | Set-Clipboard
```

Paste into the secret. Do not write the base64 to the repository.

### 6. Configure the protected GitHub environment

1. Repository **Settings** → **Environments** → create `app-store-connect-internal`.
2. Restrict deployment branches to `main` only.
3. Optionally require a reviewer.
4. Add these **environment** secrets (not repository secrets):

| Secret | Format |
|---|---|
| `APP_STORE_CONNECT_ISSUER_ID` | UUID Issuer ID from App Store Connect |
| `APP_STORE_CONNECT_KEY_ID` | 10-character Key ID |
| `APP_STORE_CONNECT_PRIVATE_KEY` | Full contents of `AuthKey_<KEY_ID>.p8`, including `BEGIN PRIVATE KEY` / `END PRIVATE KEY` lines |

Artifact-only jobs must not use this environment and must not require these three
secrets.

### 7. Publish the Apple Team ID for Universal Links

Repository **Settings** → **Secrets and variables** → **Actions** → **Variables**
for the `dev` environment used by `.github/workflows/deploy-web.yml`:

| Variable | Value |
|---|---|
| `APPLE_TEAM_ID` | `LHP9K4946H` |

This is a team identifier, not a secret. After it is set, the next Dev Pages deploy
publishes `apple-app-site-association` for both the demo and Release bundle IDs.
TestFlight HTTPS sign-in is not ready before that deploy.

### 8. Run an automated internal TestFlight release

Do this only after the secrets exist **and** an explicit approval to upload.

1. In GitHub open **Actions** then **mobile-testflight**.
2. Select branch **main** and choose **Run workflow**.
3. Leave **Upload to App Store Connect / TestFlight internal testing** enabled.
4. Leave the build number blank so the workflow queries App Store Connect.
5. Leave the display version blank to use `1.0`.

After the protected environment is approved, the publishing job selects
`highest existing integer CFBundleVersion + 1` (or `1` when no builds exist),
builds and signs a Development IPA, rechecks that the number is still unused,
uploads with `xcrun altool`, then polls Apple processing. Only a `VALID` build is
assigned to **CatchButDontForget**.

The workflow distinguishes three outcomes in the log:

- **Uploaded** — Apple accepted the IPA. Testers cannot install it yet.
- **Processing failed** — `INVALID` or `FAILED`. Testers cannot install it.
- **Available to the internal group** — processing is `VALID` and the build is
  assigned to **CatchButDontForget**.

If processing is still running when the job times out, the run **fails**. That does
not mean the upload was lost; it means testers are not yet eligible. Check App Store
Connect and rerun only if a new build number is required.

Runs share one non-cancelling concurrency group. There is no automatic retry and no
App Store review submission.

### 9. Generate a signed IPA without publishing

1. Run **mobile-testflight** from `main`.
2. Disable **Upload to App Store Connect / TestFlight internal testing**.
3. Optionally supply a CFBundleVersion and display version. Blank values use the
   project file (`1` and `1.0` for iOS Release).
4. Download `catch-ios-internal-testing` from the completed workflow.

Artifact-only mode runs without `app-store-connect-internal`, App Store Connect API
secrets, environment approval or `altool`.

### 10. Versioning behaviour

Published runs query App Store Connect build history and use one greater than the
highest integer `CFBundleVersion`. Non-integer Apple build numbers fail the run
instead of inventing a dotted bump. A second query immediately before upload fails
if another actor consumed the selected number.

`CFBundleShortVersionString` stays `1.0` unless you override it. That is the App
Store version testers see. It is not incremented per upload.

### 11. Install on a real iPhone

After Apple processing finishes and the build appears in **CatchButDontForget**:

1. Install TestFlight on the iPhone.
2. Sign in with the account holder Apple ID already in that group, or add another
   internal tester in App Store Connect.
3. Install **Catch, But Don't Forget**.

Do not sideload the unsigned simulator build from `mobile-build-test.yml`.

## Unsigned simulator CI

Pull requests and pushes to `main` / `develop` run `.github/workflows/mobile-build-test.yml`
on `macos-26`. That job:

- selects Xcode 26.5 (`/Applications/Xcode_26.5.app`);
- installs MAUI Android and iOS workloads at set `10.0.300.3` for .NET SDK `10.0.300`;
- runs `FishingLogBook.Mobile.Tests` for `net10.0`;
- builds `net10.0-ios` Debug for `iossimulator-arm64` with code signing disabled.

The same SDK, workload set and Xcode 26.5 pin is used by
`.github/workflows/mobile-testflight.yml`. Xcode 26.5 is required by that workload and
needs macOS 26.2+, so these jobs do not use the `macos-15` runner default (Xcode 16.4).

Success of the unsigned job means the iOS target compiled. It is not a simulator
runtime, TestFlight or physical-device result.

## Out of scope

- Public App Store review or production release
- Production Cognito or production associated domains
- Creating or applying Cognito Terraform
- Recreating the app, bundle ID or `CatchButDontForget` group
- A local Mac for CI signing or upload
