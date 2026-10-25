# PR #248 completion evidence — 25 October 2026

Follow-up for #240, part of #236. No UI redesign, ADR change, application-code change
or subsequent-ticket work is included.

## One Rider window, separate build boundaries

`FishingLogBook.sln` now contains `FishingLogBook.Mobile` under `src` and
`FishingLogBook.Mobile.Tests` under `tests`, with Debug/Release configurations.
Open that solution in Rider; select the mobile project's Android target when running
on the emulator. The full solution build requires Android tooling.

`FishingLogBook.Mobile.slnx` and its Android workflow are unchanged.
`FishingLogBook.Web.slnf` selects all 16 original web/backend projects and test
projects from the main solution, excluding both mobile projects. Web/backend CI,
deployment validation and authenticated browser E2E restore use this filter. Direct
Web/API publish commands and the API Dockerfile remain unchanged; the Dockerfile
restores/publishes the API project rather than the whole solution.

Production deployment can check out historical release tags. Its validation selects
the filter when present and falls back to the original solution for older tags that
predate the filter/mobile projects. No deployment was run.

The filter was restored successfully in `mcr.microsoft.com/dotnet/sdk:10.0-noble`:
`dotnet workload list` reported **zero installed workloads**. Only solution/filter,
central package metadata and project files were copied into the disposable container.
This verifies the restore boundary without relying on this Windows machine's installed
MAUI workload. Keep new non-mobile projects in the filter when adding them to the solution.

## Runtime evidence

**User-reported manual validation in Rider:** Android 36 emulator launch; Catch
summary/detail rendering; existing branding; EN/FR switching including date/number
formatting; light/dark themes. These results were supplied by the user, not inferred
from compilation.

**Additional ADB-driven checks in this session:**

- Emulator `emulator-5554`, model `sdk_gphone64_x86_64`, Android 16/API 36, Google APIs
  x86_64 image, Medium Phone AVD, 1080x2400 at 420 dpi.
- Installed `com.fishinglogbook.mobile.demo`, version 0.1.0/code 1, DEBUGGABLE.
- Restored the sample list from its empty demo state; opened European perch details.
- Opened photo preview; UIAutomator showed the correct page/species and demo-image
  description. A captured screenshot was visually inspected: the existing brand
  illustration renders fully, with the explicit “not a catch photograph” caption.
- Android Back (`KEYCODE_BACK`, code 4) returned preview -> detail -> summary.
- Separately, toolbar “Navigate up” returned preview -> detail -> summary.
- Opened the incomplete sample: “No photo recorded” displayed, with no preview action;
  Back returned to the summary.

These were real emulator interactions, not host unit tests. A first batched toolbar
sequence failed its intermediate-page assertion; it was not counted as a pass.
Repeating with UI state checked before each action confirmed both toolbar transitions.
No navigation code was changed. Local screenshot: `artifacts/mobile-240-validation/photo-preview.png`
(ignored development artifact, not committed).

Commands used to inspect/control the emulator, with actions issued only after locating
the corresponding nodes in the UI hierarchy:

```powershell
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" devices -l
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell getprop ro.build.version.sdk
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell getprop ro.build.version.release
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell getprop ro.product.cpu.abi
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell am start -n com.fishinglogbook.mobile.demo/crc6421edb0463bbcc4fc.MainActivity
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell uiautomator dump /sdcard/flb-window.xml
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell cat /sdcard/flb-window.xml
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell input swipe 540 2000 540 1000 400
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell input tap 540 1900
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell input keyevent 4
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell input tap 73 136
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 shell screencap -p /sdcard/flb-preview.png
& "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" -s emulator-5554 pull /sdcard/flb-preview.png artifacts/mobile-240-validation/photo-preview.png
```

Tap positions above are evidence for this AVD/layout, not portable test selectors.
UI dumps were parsed as XML and assertions checked the destination page titles and
missing-photo state. The activity/package was already installed by Rider; no user data
was cleared and no APK was installed over the user's emulator build.

## Build/test commands and outcomes

| Exact command | Outcome |
| --- | --- |
| `dotnet restore FishingLogBook.Web.slnf` | Passed |
| `dotnet build FishingLogBook.Web.slnf --configuration Release --no-restore` | Passed, 0 warnings/errors; includes PWA |
| `dotnet test FishingLogBook.Web.slnf --configuration Release --no-build --no-restore --logger 'console;verbosity=quiet'` | 3,083 passed |
| `dotnet build FishingLogBook.sln "-p:AndroidSdkDirectory=$env:LOCALAPPDATA/Android/Sdk" "-p:JavaSdkDirectory=$env:LOCALAPPDATA/Microsoft/flb-jdk-17"` | Passed, 0 warnings/errors; all projects including Android |
| `dotnet build FishingLogBook.Mobile.slnx "-p:AndroidSdkDirectory=$env:LOCALAPPDATA/Android/Sdk" "-p:JavaSdkDirectory=$env:LOCALAPPDATA/Microsoft/flb-jdk-17"` | Passed, 0 warnings/errors |
| `dotnet test tests/FishingLogBook.Mobile.Tests/FishingLogBook.Mobile.Tests.csproj --no-build --no-restore --logger 'console;verbosity=minimal'` | 11 passed |
| `dotnet format FishingLogBook.sln --verify-no-changes --no-restore` | Passed |
| `npm test` | Initial run: 374 passed, one worker startup timeout; isolated rerun: all 405 passed across 34 files |
| `npm run test:browser` | 159 passed, 7 existing skips |

Clean Linux SDK restore proof (PowerShell):

```powershell
docker run --rm --mount "type=bind,source=$PWD,target=/source,readonly" mcr.microsoft.com/dotnet/sdk:10.0-noble sh -c 'mkdir /work; cp /source/FishingLogBook.sln /source/FishingLogBook.Web.slnf /source/Directory.Packages.props /work/; cd /source; find src tests -name "*.csproj" -exec cp --parents {} /work/ \; ; cd /work; dotnet workload list; dotnet restore FishingLogBook.Web.slnf'
```

Exit 0; 16 projects restored, no mobile restore and no installed workloads.
All workflow YAML files were parsed with Playwright's bundled YAML parser, and filter
membership was compared with every non-mobile project in the main solution: passed.
The production solution-selection shell step was extracted from its actual YAML and
executed in disposable Linux directories both with and without the filter: selected
`FishingLogBook.Web.slnf` and `FishingLogBook.sln` respectively. No deploy step ran.
The original commit's GitHub Android and web/backend jobs had also passed before this
follow-up. New-commit CI results belong to the PR; they are distinct from local evidence.

## Remaining checks

The requested preview and normal system/toolbar back paths are verified above.
Before production readiness, manually check gesture/predictive-back animation,
rapid repeated taps, rotation/background/resume, TalkBack order, large-font/narrow
layouts, physical-device sunlight readability and trimmed Release runtime. Repeat
preview/back in French/light mode if full matrix coverage is required; this session's
ADB preview/back checks used English/dark mode. Actual fish/large-photo decoding is
not exercised by the bundled illustration.

Startup, idle memory and interaction performance budgets remain **unmeasured**; no
new performance claim is made. The approved budgets and measurement procedure remain
in [the foundation document](mobile-android-foundation.md). No physical iOS/Android
device validation or iOS implementation is claimed.

## Self review

- Issue/AC: PASS for this requested completion scope: main solution integration,
  isolated Android solution, workload-free web restore, documented user validation
  and additional emulator preview/back evidence.
- Architecture/CQRS: PASS; project membership/build selection only, no runtime layering changes.
- Rules: PASS; existing PR/branch retained; no new ticket or architectural investigation.
- Security/privacy: PASS; demo data only; no secrets, real photos, permissions or persistence changes.
- Database/migrations: N/A.
- Tests/coverage: full existing .NET tests plus mobile tests passed; real emulator
  interactions cover the requested native boundary. JavaScript retry disclosed above.
- Browser: PWA regressions passed; Playwright is not used as evidence of native UI behaviour.
- Web/UI/localisation: unchanged; user-confirmed EN/FR and themes attributed accurately.
- Code smells: no conditional mobile framework tricks; filter expresses the CI boundary explicitly.
- CI/deployment: all five web/backend workflow consumers updated; historical release-tag
  fallback retained; no deployment or Terraform apply.

Findings fixed: adding mobile only to solution build configurations would not isolate
restore; a solution filter is used instead. Production release checkout may lack the
filter, so its selection handles historical tags. Older “no emulator” statements are
marked historical and superseded by this record.

Remaining deliberate gaps are the manual/performance/Release checks listed above;
none are presented as automated passes. No application code was modified to accommodate
tests, and no subsequent mobile functionality was started.
