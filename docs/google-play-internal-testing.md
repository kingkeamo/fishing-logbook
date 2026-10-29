# Google Play Internal testing for Catch

This guide prepares the first **internal beta** of Catch on a real Android device.
It is not a production or public release. The first beta uses the existing
Development API (`https://fishing-logbook-dev-api.fly.dev`) and the existing
Development Cognito user pool.

The normal workflow builds, signs, uploads and commits an Internal testing release.
A signed Android App Bundle (`.aab`) remains available as a GitHub artifact, and the
same workflow can still be run in artifact-only mode.

## Identity

| Setting | Value |
|---|---|
| Android application ID (Play / Release) | `com.catchbutdontforget.app` |
| Name on the phone | `Catch` |
| Google Play listing name | `Catch But Don't Forget` |
| Website / privacy policy | `https://catchbutdontforget.com` |
| Initial version name | `0.1.0` |
| Initial version code | `1` |
| Track | Internal testing |

Debug and emulator installs keep `com.fishinglogbook.mobile.demo` so a Play build does
not overwrite local development. iOS still uses the demo bundle ID and custom scheme.

The Play listing name is set only in Google Play Console. The repository sets the
on-device name through `ApplicationTitle`.

## What the repository already does

- Android Release builds use `com.catchbutdontforget.app`.
- The signed AAB workflow is `.github/workflows/mobile-play-bundle.yml`. It runs only
  when started by hand from `main`, always embeds `MobileEnvironment=Development`,
  preserves the `.aab` as a GitHub Actions artifact, and can publish only to the Play
  Internal testing track. It does not apply Terraform.
- Digital Asset Links generation includes both the debug package and
  `com.catchbutdontforget.app` with the Play App Signing SHA-256 fingerprint. Sign-in
  for Play installs still requires that file to be deployed on Dev Pages.
- Native HTTPS callbacks stay:
  - `https://fishing-logbook-dev.pages.dev/native/oauth2redirect`
  - `https://fishing-logbook-dev.pages.dev/native/signedout`

No Cognito Terraform change is required for the package ID change. Those HTTPS URLs
are already registered on the Development native client.

## What you must do in Google Play Console

Complete these in the Play Console for the Catch app. Do not invent certificate
fingerprints or signing passwords in the repository.

### 1. Create the application

1. Open [Google Play Console](https://play.google.com/console).
2. Create an app.
3. Default language: English (United Kingdom) or English (United States).
4. App name (listing): **Catch But Don't Forget**.
5. Type: App. Free.
6. Declarations: this is not a Play App Signing opt-out; accept Play App Signing.

### 2. Configure Play App Signing

Play resigns the installed APK with Google's app-signing certificate. Digital Asset
Links must use **that** certificate, not the upload key.

After the app exists:

1. Open the app in Play Console.
2. Go to **Test and release** → **App integrity** (sometimes labelled **App signing**
   under Setup).
3. Confirm Play App Signing is enabled.
4. Leave the upload key as the key you create in the next step. Do not replace it
   unless Play has instructed you to.

### 3. Create and back up the upload keystore

Create this once on a trusted machine. Store the file and both passwords in a password
manager and an offline encrypted backup. Losing the upload key requires a Play Console
reset.

```powershell
keytool -genkeypair -v `
  -keystore $env:USERPROFILE\secure\catch-upload.keystore `
  -alias catch-upload `
  -keyalg RSA `
  -keysize 4096 `
  -validity 10950 `
  -storetype PKCS12
```

`keytool` will prompt for the keystore password, key password, and certificate name
fields. Use a real organisation name. Do not commit the keystore, a base64 copy, or
the passwords.

### 4. Obtain Google's App Signing SHA-256 fingerprint

This value is available only after Play App Signing is configured for the app.

1. Play Console → Catch → **Test and release** → **App integrity**.
2. Open **App signing key certificate** (not Upload key certificate).
3. Copy **SHA-256 certificate fingerprint**.
4. Keep the colons and uppercase hex (example form only:
   `AB:CD:EF:...` with 32 bytes). Do not paste a made-up value into git.

Until this fingerprint is in the deployed `assetlinks.json`, a Play-installed Catch
build **cannot** complete verified HTTPS sign-in.

### 5. Update and deploy Digital Asset Links

The Play App Signing SHA-256 is already in
`src/FishingLogBook.Web/build/app-links.dev.json` for `com.catchbutdontforget.app`.
Keep the debug package and its existing fingerprint. Do not change `callbackPaths` or
the iOS `bundleId`. If Play rotates the app-signing certificate, replace that fingerprint
with the new value from Console (uppercase, colon-separated).

Merge to the branch that Dev Pages deploys from so `.github/workflows/deploy-web.yml`
regenerates `https://fishing-logbook-dev.pages.dev/.well-known/assetlinks.json`.

Verify:

1. The live file contains `"package_name": "com.catchbutdontforget.app"`.
2. The fingerprint matches Play Console exactly.
3. Optional: [Google's statement list generator](https://developers.google.com/digital-asset-links/tools/generator)
   for host `fishing-logbook-dev.pages.dev` and package `com.catchbutdontforget.app`.

Do not treat release authentication as ready before this verification succeeds.

### 6. Configure GitHub Actions signing secrets

Repository **Settings** → **Secrets and variables** → **Actions**. Create:

| Secret | Value |
|---|---|
| `ANDROID_UPLOAD_KEYSTORE_BASE64` | Base64 of the `.keystore` file |
| `ANDROID_UPLOAD_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_UPLOAD_KEY_ALIAS` | Alias, for example `catch-upload` |
| `ANDROID_UPLOAD_KEY_PASSWORD` | Key password |

Encode the keystore on Windows:

```powershell
[Convert]::ToBase64String(
  [IO.File]::ReadAllBytes("$env:USERPROFILE\secure\catch-upload.keystore")
) | Set-Clipboard
```

Paste into the secret. Do not write the base64 to the repository. Do not print the
secrets in workflow logs; the workflow reads passwords through `env:` MSBuild
properties.

### 7. Configure Google Play Developer API access

Do this once, in this order. These steps create a dedicated identity but no long-lived
service-account key.

1. In Google Cloud Console, create or select a project dedicated to release automation.
2. Enable **Google Play Android Developer API**, **IAM Service Account Credentials
   API**, and **Security Token Service API**.
3. Create a service account named `catch-play-publisher`. Do not create or download a
   JSON key.
4. In Play Console, open **Users and permissions** and invite the service-account email.
5. Limit its app access to **Catch But Don't Forget**.
6. Grant only **View app information (read-only)** and **Release apps to testing
   tracks**. Do not grant production-release, financial, order, subscription or
   account-admin permissions.

Equivalent Cloud Shell setup (replace `PROJECT_ID`):

```bash
gcloud config set project PROJECT_ID
gcloud services enable \
  androidpublisher.googleapis.com \
  iamcredentials.googleapis.com \
  sts.googleapis.com
gcloud iam service-accounts create catch-play-publisher \
  --display-name="Catch Play Internal publisher"
```

The Android Publisher permission is granted in Play Console, not through a broad Google
Cloud IAM role.

### 8. Configure GitHub OIDC with Workload Identity Federation

Continue in the same Google Cloud project. The provider condition below trusts only
`kingkeamo/fishing-logbook` runs dispatched from `main`.

```bash
PROJECT_ID="your-project-id"
PROJECT_NUMBER="$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')"
SERVICE_ACCOUNT="catch-play-publisher@${PROJECT_ID}.iam.gserviceaccount.com"

gcloud iam workload-identity-pools create github \
  --project="$PROJECT_ID" \
  --location=global \
  --display-name="GitHub Actions"

gcloud iam workload-identity-pools providers create-oidc fishing-logbook-main \
  --project="$PROJECT_ID" \
  --location=global \
  --workload-identity-pool=github \
  --display-name="Fishing Logbook main" \
  --issuer-uri="https://token.actions.githubusercontent.com" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.ref=assertion.ref" \
  --attribute-condition="assertion.repository=='kingkeamo/fishing-logbook' && assertion.ref=='refs/heads/main'"

gcloud iam service-accounts add-iam-policy-binding "$SERVICE_ACCOUNT" \
  --project="$PROJECT_ID" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/github/attribute.repository/kingkeamo/fishing-logbook"

gcloud iam workload-identity-pools providers describe fishing-logbook-main \
  --project="$PROJECT_ID" \
  --location=global \
  --workload-identity-pool=github \
  --format='value(name)'
```

Save the final provider resource name. It has this form:

```text
projects/123456789/locations/global/workloadIdentityPools/github/providers/fishing-logbook-main
```

Normal Workload Identity service-account impersonation requires
`roles/iam.workloadIdentityUser`; do not add a service-account key.

### 9. Configure the protected GitHub environment

1. In repository **Settings** open **Environments** and create
   `google-play-internal`.
2. Restrict deployment branches to `main` only.
3. Add these environment variables:

| Variable | Value |
|---|---|
| `GOOGLE_PLAY_WORKLOAD_IDENTITY_PROVIDER` | Full provider resource name returned above |
| `GOOGLE_PLAY_SERVICE_ACCOUNT` | `catch-play-publisher@PROJECT_ID.iam.gserviceaccount.com` |

4. Keep the four existing Android upload-signing secrets available to this environment,
   either as environment secrets or existing repository secrets.
5. Optionally require a reviewer for the environment as a second release confirmation.

OIDC tokens are short-lived. There is no Google service-account JSON key to store in
GitHub.

### 10. Run an automated Internal testing release

1. In GitHub open **Actions** then **mobile-play-bundle**.
2. Select branch **main** and choose **Run workflow**.
3. Leave **Publish to Google Play Internal testing** enabled.
4. Optionally enter a version name and release notes. If the version name is blank, the
   workflow uses `<project-version>-internal.<version-code>`.

The workflow authenticates with GitHub OIDC, creates a Play edit, lists every existing
AAB and legacy APK version code, selects `highest + 1`, builds and signs the Development
AAB, uploads it only to `internal` with status `completed`, validates the edit and commits
it. The signed `catch-android-internal-testing` artifact remains downloadable for 14
days.

Runs share one non-cancelling concurrency group, so two workflow runs cannot select the
same next version. A manual Play Console change during the run can invalidate the edit;
Play then rejects the run safely and it can be rerun. The commit also refuses to replace
changes already in review.

Missing OIDC configuration, insufficient Play permissions, rejected bundles,
validation errors and incomplete commits fail the workflow clearly.

### 11. Generate a signed AAB without publishing

This is the fallback when Play API access is unavailable or a bundle needs manual
review.

1. Run **mobile-play-bundle** from `main`.
2. Disable **Publish to Google Play Internal testing**.
3. Optionally supply an artifact-only version code and version name. Blank values use
   the project file.
4. Download `catch-android-internal-testing` from the completed workflow.

Artifact-only mode does not authenticate to Google Play and does not create or commit a
Play edit.

### 12. Legacy manual signed AAB procedure

1. Run **mobile-play-bundle** from `main` with publishing disabled.
2. Supply a version code higher than every version already uploaded to Play.
3. Download the `catch-android-internal-testing` artifact. It contains the `.aab`.

The workflow always sets `-p:MobileEnvironment=Development`. It never points at
localhost or the emulator alias `10.0.2.2`.

Local equivalent, after the keystore exists (do not put passwords on the command
line):

```powershell
$env:ANDROID_UPLOAD_KEYSTORE_PASSWORD = 'use a secret, do not commit'
$env:ANDROID_UPLOAD_KEY_PASSWORD = 'use a secret, do not commit'
dotnet publish src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj `
  -f net10.0-android -c Release `
  -p:RuntimeIdentifier=android-arm64 `
  -p:MobileEnvironment=Development `
  -p:AndroidPackageFormats=aab `
  -p:AndroidKeyStore=true `
  "-p:AndroidSigningKeyStore=$env:USERPROFILE\secure\catch-upload.keystore" `
  -p:AndroidSigningStorePass=env:ANDROID_UPLOAD_KEYSTORE_PASSWORD `
  -p:AndroidSigningKeyAlias=catch-upload `
  -p:AndroidSigningKeyPass=env:ANDROID_UPLOAD_KEY_PASSWORD
```

### 13. Upload the fallback artifact manually

1. Play Console → **Test and release** → **Testing** → **Internal testing**.
2. Create a release.
3. Upload the `.aab`.
4. Play will reject the package if the application ID, version code, or signing does
   not match. Do not switch to production.

### 14. Add tester email addresses

Internal testing → testers → add the Google accounts that should install Catch,
including the Samsung phone's account.

### 15. Obtain the opt-in installation link

Internal testing shows an opt-in URL. Open it while signed into a tester Google
account, accept the test, then install from Play Store.

### 16. Install Catch on a real Android device

Install from Play Store after opt-in. Do not sideload the CI APK from
`mobile-build-test.yml`; that job is unsigned ARM64 validation, not a Play build.

After Digital Asset Links are deployed, smoke-test:

- Sign in and sign out
- Native onboarding
- Profile
- Location permission
- Authenticated API access against `https://fishing-logbook-dev-api.fly.dev`

### 17. Versioning behaviour

Published runs query Play inside the release edit and build with one greater than the
highest uploaded AAB or legacy APK version code. This includes inactive and superseded
artifacts and does not rely on the GitHub run number. The `application_version` input is
intentionally limited to artifact-only fallback builds.

`ApplicationDisplayVersion` is the human-readable name testers see. Enter one for a
specific release, or let the workflow create a unique internal name from the project
version and selected Play version code.

## Local emulator development

Keep using a Debug install:

```powershell
dotnet build src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj `
  -t:Run -f net10.0-android -c Debug `
  "-p:AndroidSdkDirectory=$env:LOCALAPPDATA/Android/Sdk" `
  "-p:JavaSdkDirectory=$env:LOCALAPPDATA/Microsoft/flb-jdk-17"
```

That package remains `com.fishinglogbook.mobile.demo`. Uninstall a previous Debug
build before installing if Fast Deployment leaves a broken incremental install.

## Out of scope

- Production Play track and production Cognito
- A second backend environment for this beta
- Creating or applying Cognito Terraform
- Production, closed-testing or open-testing uploads
