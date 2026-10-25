# Android foundation — #240

Implements the native XAML/C# decision from #239; does not change ADR 0001.
This is a demo application, not a connected mobile client. It references only
`FishingLogBook.Shared` (`CatchDto`) and leaves the PWA, APIs and schema unchanged.

**Current status (25 October 2026):** the user has validated the app in Rider on an
Android 36 emulator, and photo preview/system and toolbar back navigation have now
been checked through ADB. See [PR completion evidence](mobile-pr248-completion.md)
for current solution entry points, results and the remaining manual checks. The
24 October tooling and validation record below is retained as historical evidence.

## Project and behaviour

- `FishingLogBook.sln` contains all projects for Rider. `FishingLogBook.Mobile.slnx`
  retains the isolated Android host/tests build; `FishingLogBook.Web.slnf` filters
  the main solution to web/backend projects without MAUI requirements. Android
  minimum API 26, target API 36, .NET 10.
- `src/FishingLogBook.Mobile/Features/Catch/` owns native summary, detail and image
  preview pages, their presentation model and the sample-list view model.
- Shell provides the navigation stack and native toolbar/system back handling.
  Single-use object navigation parameters avoid serialising demo DTOs into URLs.
  Navigation buttons guard repeated taps during transitions.
- One sample has species, recorded UTC time, metric size, a fictitious place, notes
  and a bundled image. Another deliberately omits optional values. A labelled demo
  control exposes the empty-list state and restores the same sample identities.
- The preview uses existing **brand illustration**, labelled in both languages as
  an illustration rather than a fish photograph. There is no new photo asset,
  remote image request, camera/gallery operation, saving or synchronisation.
- EN/FR resources update in-app. French device locales map to fr-FR; other locales
  fall back to en-GB. Native launcher labels have Android English/French resources.
  Native OS dialogs/back announcements follow the device language. Demo language,
  theme and list selection are memory-only and reset on process restart.
- System/light/dark appearance is selectable. App resources are loaded before
  resolving Shell/pages. UI text notifications use `Item`, which MAUI's indexer
  bindings recognise (not the collection-style `Item[]` notification).
- The additional plain `net10.0` target excludes MAUI pages/platform entry points
  and compiles the **same** presentation/resource code for host unit tests. It is
  not a second app or a shared UI library. This follows Microsoft's
  [MAUI application unit-testing approach](https://learn.microsoft.com/dotnet/maui/deployment/unit-testing).

## Design system and asset mapping

`Resources/Styles/DesignSystem.xaml` centralises the palette, typography, spacing,
card/button/input styles and theme colours. Pages use these resources rather than CSS.

| Existing source | Native use |
| --- | --- |
| `Web/wwwroot/icon-512.png` | Linked MAUI launcher icon, density/adaptive resources generated at build time |
| `Web/wwwroot/images/brand/brand-mark-transparent.png` | Linked splash and `brand_mark.png` demo illustration; resized by MAUI |
| `Web/wwwroot/images/brand/brand-horizontal-transparent.png` | Linked header `brand_horizontal.png`, AspectFit on a white surface in both themes |
| `docs/design/branding/catch-but-dont-forget-brand-board.png` | Navy `#0B1F3A`, teal `#0F766E`, sky `#E0F2FE`; no invented replacement logo |
| `Web/wwwroot/css/app.css` | Existing Roboto-first body stack maps to Android's system Roboto; accessibility scaling retained |
| `Web/Common/Icons/FishingLogBookIcons.cs` | Fishing-trip SVG inspected; not appropriate to these Catch screens, so not copied. Shell supplies its native back icon |

The brand board names Bebas Neue/Inter, but there are no bundled font files in the
repository. This ticket uses the shipped web font convention/native system font
instead of downloading unreviewed font files or reproducing the logo in text.
Linking originals shares assets without referencing the Web executable or moving files.

Body/action text is 18 sp, secondary text 16 sp, headings 22/28 sp. Spacing uses
8/16/24 dp, cards have 16 dp corners, buttons 12 dp corners. Inputs and buttons
request at least 48 dp touch dimensions. Scrollable pages, safe-area insets, wrapping
labels, semantic headings, meaningful button descriptions and image descriptions
support outdoor use and assistive technology. No meaning depends on colour alone.

Calculated sRGB text contrast: white/teal 5.47:1, navy/white 16.52:1,
sky/dark surface 11.76:1, secondary text on light/dark cards 7.58:1/9.09:1.
These are token calculations, **not** proof of rendered contrast, sunlight legibility,
TalkBack order or large-font layout on a device.

## Toolkit decision for this implementation

| Candidate | Compatibility/licence checked | Concrete value here / decision |
| --- | --- | --- |
| Native MAUI controls | `Microsoft.Maui.Controls` 10.0.120, .NET 10; [MIT source](https://github.com/dotnet/maui) | Shell, CollectionView, Border, Image, Picker and Button cover the demo. Selected; one centrally pinned package |
| CommunityToolkit.Maui | [15.0.1](https://www.nuget.org/packages/CommunityToolkit.Maui/15.0.1) includes net10.0-android36.0 and requires MAUI >=10.0.90; [MIT](https://github.com/CommunityToolkit/Maui/blob/main/LICENSE) | Behaviours, converters and popup controls offer no missing capability for these three screens. Not installed |
| Syncfusion **Toolkit** | [1.0.10 package](https://www.nuget.org/packages/Syncfusion.Maui.Toolkit/1.0.10), [.NET 10 support](https://help.syncfusion.com/maui-toolkit/release-notes/v1.0.8), [MIT/free Toolkit](https://www.syncfusion.com/net-maui-toolkit) | Rich pickers, numeric editors, charts and other controls are useful for more complex flows, but unnecessary here. Not installed; no commercial Syncfusion suite or licence key |

Compatibility evaluation is based on published package metadata/documentation;
neither optional toolkit was compiled or benchmarked. Reconsider only for a concrete
control requirement, not for a second styling framework.

## Reproduce the build

Validated host: Windows 10.0.26300 x64; .NET SDK 10.0.300; workload manifests
`maui-android 10.0.0/10.0.100`, `android 36.1.2/10.0.100`.
The Controls package is independently pinned at 10.0.120 in `Directory.Packages.props`.
Android SDK platform 36, build tools 36.0.0, platform tools 36.0.0-13206524;
Microsoft OpenJDK **17.0.14+7**. No emulator/AVD or connected device was available
on 24 October; the 25 October emulator results supersede that limitation.

From the repository root, with .NET SDK 10.0.300 selected, run in PowerShell:

```powershell
dotnet workload install maui-android --skip-manifest-update
dotnet build src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA/Android/Sdk" "-p:JavaSdkDirectory=$env:LOCALAPPDATA/Microsoft/flb-jdk-17" -p:AcceptAndroidSDKLicenses=True
dotnet build FishingLogBook.Mobile.slnx "-p:AndroidSdkDirectory=$env:LOCALAPPDATA/Android/Sdk" "-p:JavaSdkDirectory=$env:LOCALAPPDATA/Microsoft/flb-jdk-17"
dotnet test tests/FishingLogBook.Mobile.Tests/FishingLogBook.Mobile.Tests.csproj
dotnet build src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk -p:RuntimeIdentifier=android-arm64 "-p:AndroidSdkDirectory=$env:LOCALAPPDATA/Android/Sdk" "-p:JavaSdkDirectory=$env:LOCALAPPDATA/Microsoft/flb-jdk-17"
```

Initially the user-writable JDK directory was named `jdk-21`, but the installer
selected JDK **17**, verified by `bin/java.exe -version`. It was renamed to
`flb-jdk-17` before final validation; directory names do not select JDK versions.
Existing compatible SDK/JDK locations can be passed instead. The first attempt to
install a JDK under Program Files was rejected; user-writable paths succeeded.
The workload installer also reconciled
pre-existing Visual Studio workload packs; those machine changes are not repository files.

Debug APKs are under `src/FishingLogBook.Mobile/bin/Debug/net10.0-android/`;
ARM64 Release APKs are under `bin/Release/net10.0-android/android-arm64/`.
They are local development artifacts, not store-signed releases. The independent
`mobile-build-test.yml` workflow installs the workload, checks SDK dependencies,
builds the mobile solution, runs presentation tests and builds a trimmed ARM64 APK.
It does not deploy, publish or modify existing PWA/backend workflows.

## Runtime checks and agreed budgets

The user approved these initial budgets before any measurement:

| Measure | Budget | Evidence now |
| --- | --- | --- |
| Release cold start | <=3 seconds to usable summary | Not measured |
| Idle app memory | <=200 MiB total PSS | Not measured |
| Navigation | <=100 ms response to touch | Not measured |
| Touch targets | >=48 dp | Minimum sizes defined in styles; rendered targets unverified |

For the first device session, record model, Android API/OS, ABI, screen size/density,
font scaling, app commit and Release build. Use at least five force-stopped cold
starts (`adb shell am start -W` plus visible-usability observation), report every
sample/range, and inspect `adb shell dumpsys meminfo com.fishinglogbook.mobile.demo`
after 30 seconds idle. Measure touch-to-feedback with a trace/frame recording;
distinguish immediate response from completion of Shell's animated transition.
Emulator results are indicative and must not be represented as physical-device results.

Manual matrix (see the completion evidence for the subset now verified):

1. Launch in EN and FR; verify launcher/splash/header assets, sample species,
   UTC date, size, notes and missing fields. Switch languages and verify Shell title,
   picker choices, decimal separator and empty-state text without restarting.
2. Open each sample, preview the illustration, use toolbar and Android system/gesture
   back through preview -> detail -> summary. Repeated taps must not stack duplicates.
3. System/light/dark themes, rotate, background/resume, 200% font size and narrow screen:
   check wrapping, scroll reachability, safe areas and picker selection preservation.
4. TalkBack: headings/order, readable buttons, distinct catch descriptions, missing-photo
   announcement, and no unlabelled actionable images. Check outdoor readability.
5. Force-stop/relaunch: only bundled demo state returns; no save/sync or network claim.

The initial 24 October work did not include Android runtime evidence. The completion
record adds Debug emulator navigation and visual checks; TalkBack, performance and
trimmed Release runtime are still unverified. Playwright cannot exercise MAUI's
Android view hierarchy; the existing
browser suite remains the PWA regression check, not native UI coverage.

## Security and deliberate boundaries

No API calls, credentials, authentication, storage, precise locations, permissions
requests or user data. Backup and cleartext traffic are disabled. The generated Release
manifest has no Internet/camera/location/storage permission; Debug adds Internet for
development tooling. Framework providers/receivers are MAUI dependencies, not implemented
camera/auth/connectivity flows. Package id is explicitly a `.demo` identity.

No migrations, cloud changes, store publication, production signing or deployment.
iOS, authentication, persistence, synchronisation, camera/gallery and all subsequent
tickets remain untouched. Existing ADR and PWA assets are unchanged.

## Validation record — 24 October 2026

Build/setup commands above are the exact final commands used. Additional commands:

| Command | Result |
| --- | --- |
| `dotnet --version`; `dotnet workload list` | Versions recorded above; MAUI Android installation succeeded |
| `dotnet format FishingLogBook.Mobile.slnx --no-restore` | Applied repository formatting during implementation |
| `dotnet format FishingLogBook.Mobile.slnx --verify-no-changes --no-restore` | Passed after review fixes |
| `dotnet format FishingLogBook.sln --verify-no-changes --no-restore` | Passed after review fixes |
| Mobile solution build with explicit SDK/JDK paths above | Passed, 0 warnings/errors; Android APK and net10.0 test target |
| ARM64 Release build above | Final build passed, 0 warnings/errors, including trimming and AOT compilation; APK produced |
| `dotnet test tests/FishingLogBook.Mobile.Tests/FishingLogBook.Mobile.Tests.csproj --logger 'console;verbosity=minimal'` | Initial 10 tests passed; 11 after UTC regression added |
| `dotnet test tests/FishingLogBook.Mobile.Tests/FishingLogBook.Mobile.Tests.csproj --no-build --no-restore --logger 'console;verbosity=minimal'` | Final 11 passed, 0 skipped |
| `dotnet build FishingLogBook.sln --no-restore` | Passed, 0 warnings/errors, including Web/PWA |
| `dotnet test FishingLogBook.sln --no-build --no-restore --logger 'console;verbosity=quiet'` | 3,083 passed: Web 1,900; Application 597; Infrastructure 289; API 269; Shared 18; migrations 10 |
| `npm test` | 405 passed across 34 files |
| `npm run test:browser` | Chromium/WebKit: 159 passed, 7 existing skips (offline shell/quota scenarios); Node colour/deprecation warnings only |
| `git diff --cached --check` | Passed after removing trailing blank resource lines |

Tooling probes executed:

```powershell
& "$env:LOCALAPPDATA/Microsoft/flb-jdk-17/bin/java.exe" -version
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" version
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" devices -l
Test-Path "$env:LOCALAPPDATA/Android/Sdk/emulator/emulator.exe"
Get-ChildItem "$env:USERPROFILE/.android/avd" -ErrorAction SilentlyContinue -Name
& "$env:LOCALAPPDATA/Android/Sdk/build-tools/36.0.0/aapt2.exe" dump badging src/FishingLogBook.Mobile/bin/Release/net10.0-android/android-arm64/com.fishinglogbook.mobile.demo-Signed.apk
```

ADB reported no devices; emulator path was false and no AVD was present. The existing
Program Files Android SDK also had no emulator. APK inspection confirmed the launch
activity, EN/FR app labels, minimum 26/target 36 and only the internal AndroidX receiver
signature permission in Release. This is package inspection, not execution.

Initial `dotnet build src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj -f net10.0-android`
failed with XA5207 (platform 36 missing). `InstallAndroidDependencies` with
`AndroidSdkDirectory=C:/Program Files (x86)/Android/android-sdk` and
`JavaSdkDirectory=C:/Program Files (x86)/Android/openjdk/jdk-21` failed because the
installer requires a non-administrator JDK path. User-writable installation succeeded.
An initial mobile solution build then failed with XC0009 for a nested implicit-style
resource key; replacing it with the named `BodyText` base style fixed compilation.

Workflow syntax was parsed with the YAML parser already bundled in Playwright:

```powershell
node -e "const fs=require('fs'); const {yaml}=require('./node_modules/playwright-core/lib/utilsBundle.js'); const workflow=yaml.parse(fs.readFileSync('.github/workflows/mobile-build-test.yml','utf8')); if (!workflow.jobs.android.steps.length) process.exit(1); console.log('Mobile workflow YAML parsed');"
```

This passed. A preceding probe using `require('yaml')` failed with MODULE_NOT_FOUND;
no dependency was added. Parsing does not validate GitHub runner execution. Remote CI
results are separate from these local results and must be checked on the PR.

## Initial self review — 24 October 2026

- Issue/AC: source/build/test foundation implemented. Android runtime acceptance
  remains unverified because no device/emulator was available; keep the PR draft.
- Architecture/CQRS: PASS. Native pages, Shared-only project reference, no server
  CQRS/persistence changes or UI extraction; original architecture decision preserved.
- Rules: PASS. Feature-owned types, central packages, repository test stack and
  WhenTesting layout; source changes authorised by #240 implementation request.
- Security/privacy: PASS within demo scope. Bundled fictitious data only; Release
  permission manifest inspected; no credentials or user state processed.
- Database/migrations: N/A; no database or schema edits.
- Tests/coverage: PASS for presentation semantics and existing regressions. Tests
  use real DTOs/resources/models; they do not substitute for Android handler tests.
- Browser: existing PWA regression suite passed. Native flows cannot run in Playwright;
  no browser host or test authentication was added. Device automation can be considered
  after the foundation is reviewed; no follow-up ticket was started.
- Web/UI/localisation: EN/FR resources, culture formatting and resource-key parity
  tested. XAML, styles and assets compile; rendered layout/TalkBack remain unverified.
- Code smells: no repository/service abstractions introduced for static demo data.
  Test target shares presentation source without introducing a separate library.
- CI/deployment: independent Android build/test workflow added and syntax parsed;
  remote run pending. No deployment, migration, Terraform apply or publication.

Findings discovered and fixed:

1. **BLOCKER:** resolving pages before Application resources would fail startup.
   Resolve Shell inside `CreateWindow`, after resource initialisation.
2. **SHOULD FIX:** an offset timestamp could disagree with its UTC label. Convert
   to UTC explicitly and retain a non-zero-offset regression test.
3. **SHOULD FIX:** `Item[]` does not refresh MAUI's dynamic indexer bindings. Use
   `Item`, checking the [MAUI binding implementation](https://github.com/dotnet/maui/blob/10.0.120/src/Controls/src/Core/BindingExpression.cs)
   and asserting the emitted notification in host tests.
4. **SHOULD FIX:** generated launcher assets were not explicitly wired in the manifest.
   Set icon/round-icon and localise the Android app label; inspect the generated APK.
5. **SHOULD FIX:** literal language choices and a narrow measurement row weakened
   localisation/large-font support. Use resource-backed endonyms and stacked labels.

Remaining deliberate gaps: native runtime/back/rotation/theme/TalkBack checks;
startup/memory/interaction measurements; actual fish-photo decoding and large media;
preference/state persistence; production signing and iOS. The demo illustration and
memory-only settings are explicit, and all later-ticket functionality is deferred.
