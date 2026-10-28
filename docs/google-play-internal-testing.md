# Google Play Internal testing for Catch

This guide prepares the first **internal beta** of Catch on a real Android device.
It is not a production or public release. The first beta uses the existing
Development API (`https://fishing-logbook-dev-api.fly.dev`) and the existing
Development Cognito user pool.

Repository work stops at a signed Android App Bundle (`.aab`) artifact. Google Play
Console steps are manual.

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
  when started by hand, always embeds `MobileEnvironment=Development`, and uploads the
  `.aab` as a GitHub Actions artifact. It does not upload to Play and does not apply
  Terraform.
- Digital Asset Links generation already knows the Play package ID. It **does not**
  emit a Play statement until the real Play App Signing SHA-256 fingerprint is inserted.
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

In `src/FishingLogBook.Web/build/app-links.dev.json`, find the Play app entry:

```json
{
  "packageName": "com.catchbutdontforget.app",
  "sha256CertFingerprints": []
}
```

Replace the empty array with the Play App Signing SHA-256 from Console:

```json
{
  "packageName": "com.catchbutdontforget.app",
  "sha256CertFingerprints": [
    "PASTE-PLAY-APP-SIGNING-SHA256-HERE"
  ]
}
```

Keep the debug package and its existing fingerprint. Do not change `callbackPaths` or
the iOS `bundleId`.

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

### 7. Generate the signed AAB

1. GitHub → **Actions** → **mobile-play-bundle**.
2. **Run workflow**.
3. For the first upload leave version inputs empty (project file is `0.1.0` / `1`).
4. Download the `catch-android-internal-testing` artifact. It contains the `.aab`.

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

### 8. Upload to the Internal testing track

1. Play Console → **Test and release** → **Testing** → **Internal testing**.
2. Create a release.
3. Upload the `.aab`.
4. Play will reject the package if the application ID, version code, or signing does
   not match. Do not switch to production.

### 9. Add tester email addresses

Internal testing → testers → add the Google accounts that should install Catch,
including the Samsung phone's account.

### 10. Obtain the opt-in installation link

Internal testing shows an opt-in URL. Open it while signed into a tester Google
account, accept the test, then install from Play Store.

### 11. Install Catch on a real Android device

Install from Play Store after opt-in. Do not sideload the CI APK from
`mobile-build-test.yml`; that job is unsigned ARM64 validation, not a Play build.

After Digital Asset Links are deployed, smoke-test:

- Sign in and sign out
- Native onboarding
- Profile
- Location permission
- Authenticated API access against `https://fishing-logbook-dev-api.fly.dev`

### 12. Increment the version code for later betas

Every Play upload needs a higher `ApplicationVersion` (version code). Either:

- bump `<ApplicationVersion>` in `src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj`,
  or
- pass **application_version** when starting `mobile-play-bundle`.

`ApplicationDisplayVersion` (`0.1.0`, then `0.1.1`, …) is the name testers see. It
does not need to change on every upload, but the version code must.

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
- Automatic Play uploads from GitHub Actions
