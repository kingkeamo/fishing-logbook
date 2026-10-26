# Native UX foundation — #249

Native .NET 10 MAUI XAML/C# on the existing mobile project. This implements the
accepted architecture; it does not change the ADR, PWA, API contracts or database.
The journal is still fictitious. There is no account, network client, catch storage,
camera/gallery, ruler, scales or custom fishing loader.

## Structure and decisions

- Shell owns four bottom tabs: Catches, Trips, Explore and Settings. Trips/Explore
  explicitly explain that they are not implemented. The flyout has only About and,
  in Debug, the developer showcase. Primary destinations are not duplicated there.
- `NavigationService` validates destinations and catch/photo requirements and
  suppresses overlapping/repeated pushes. `IRouteNavigator` is the small native
  boundary; Shell remains the authoritative stack, including system/toolbar Back.
  Catch parameters are single-use object parameters, never serialized into URLs.
- `InteractionService` provides localised success/info/warning/error notifications,
  confirmations (including destructive intent), and cancellable modals. It prevents
  overlapping dialogs. `NativeInteractionPresenter` handles UI-thread dispatch,
  screen-reader announcements, snackbar colours, popup results and actual dismissal.
  Cancellation closes the popup before releasing the dialog gate.
- `ContentStateModel` and `ContentStateView` share loading, empty, error, offline and
  planned-destination presentation. The Debug showcase applies simulated states to
  the Catch journal. Retry restores samples; it does not pretend to perform a sync.
- `PreferenceService` owns EN/FR and system/light/dark choices. The native adapter
  uses MAUI Preferences for these two non-sensitive settings only. Startup reads
  without rewriting values; malformed saved settings fall back safely. Language
  changes preserve appearance. Resource initialization still precedes Shell creation.
- The design dictionary owns the PWA's primary purple, supporting brand colours,
  typography, spacing, radii, elevation, buttons, inputs and interaction states.
  Text scales with Android; actions wrap and request at least 48dp. Scrollable
  layouts accommodate smaller screens. Tabs use native selection and accessibility
  behaviour. The native Shell app bar uses the same purple as the PWA app bar.
- Catch summary cards follow the PWA's compact 80–104px photo-led pattern. They are
  grouped by day and expose time, method and available measurements through one
  accessible card action. Search and stable method filters run against the displayed
  fictitious data. Detail and immersive preview use the same hierarchy and surfaces.
- The brown-trout reference photograph is the public-domain USFWS image supplied in
  `test-images`; attribution and provenance are recorded beside the bundled copy.
  Salmon deliberately demonstrates the missing-photo state because no suitable
  salmon image was present. Generated imagery is not bundled. Photograph captions
  distinguish the real reference photograph from the fictitious catch data.
- The launcher/splash, wordmark and existing tab iconography remain. The flyout uses
  the original wordmark without a permanent Catch banner. The Trips icon reuses the
  existing web FishingTrip SVG. System Roboto remains the existing font choice. Real
  user content is never translated.
- Existing full Rider solution, isolated mobile solution, and web-only solution
  filter are preserved. The toolkit is referenced only by the Android target.
  Portable presentation tests still compile the same code through `net10.0`.

## Toolkit evaluation

Use **CommunityToolkit.Maui 15.0.1 (MIT)** for its native snackbar and typed popup.
These replace handwritten native alert handlers and provide dismiss/result hooks.
The package supports .NET 10 and the existing MAUI 10.0.120 dependency. No toolkit
view-model framework or new application-wide mediator is needed.

The **free Syncfusion MAUI Toolkit (MIT)** was evaluated for bottom sheets,
navigation drawers, tabs and popup controls. Shell already supplies the required
navigation and CommunityToolkit supplies the needed shared interactions; a second
control library does not add enough value here. No Syncfusion package or commercial
Syncfusion suite/license is introduced. Reconsider a specific control only when an
actual later requirement warrants it.

Sources: [CommunityToolkit package/license](https://www.nuget.org/packages/CommunityToolkit.Maui/15.0.1),
[snackbar](https://learn.microsoft.com/dotnet/communitytoolkit/maui/alerts/snackbar),
[typed popup/dismissal results](https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/popup/popup-result),
[Syncfusion free toolkit/control list/license](https://github.com/syncfusion/maui-toolkit).
The installed toolkit XML explicitly states that cancelling its result wait does
**not** close the popup. The native adapter therefore owns that cleanup.

## Automated coverage

Mobile unit tests cover route validation and exact paths, missing-photo rejection,
repeat-tap suppression, back/root behaviour, recovery after navigation failure,
notification variants/localised labels/cancellation, confirmation accept/cancel,
dialog concurrency/failure, modal cancellation, preference validation/persistence
failures/reload inputs, culture formatting, retained sample localisation, unchanged
user content, resource parity, search matching, combined method filtering, clearing
filters, date grouping/order, and the full state-to-journal restoration sequence.

The ADB regression runner exercises the actual installed native app:

```powershell
node tools/mobile/validate-ux.cjs navigation
node tools/mobile/validate-ux.cjs preferences
node tools/mobile/validate-ux.cjs interactions
node tools/mobile/validate-ux.cjs states
node tools/mobile/validate-ux.cjs filters
node tools/mobile/validate-ux.cjs accessibility
```

Install a **Debug** APK first. It defaults to the existing Windows SDK and
`emulator-5554`; set `ADB_PATH` and `ANDROID_SERIAL` for another installation.
Use a disposable demo emulator: the runner changes app language/appearance and
simulated journal state. The accessibility phase restores the original Android
font scale, display-size override and rotation settings in a finally block.
Screenshots and the last UI hierarchy are written to
`artifacts/mobile-249-validation/` (ignored generated artifacts).
No Appium, browser host, test authentication or production testing hooks are added.

## Visual review evidence

The PWA was run locally and captured as the design reference before the native
polish pass. The checked-in images make the comparison available to reviewers
without requiring the emulator artifacts:

| Evidence | Screenshot |
|---|---|
| MAUI before polish | [Catch summary](images/mobile-249/before-summary.png) |
| Existing PWA reference | [PWA Catch list](images/mobile-249/pwa-reference.png) |
| MAUI after polish | [English/light summary](images/mobile-249/after-summary-en-light.png) |
| Localisation and theme | [French/dark summary](images/mobile-249/after-summary-fr-dark.png) |
| Catch hierarchy | [English/light detail](images/mobile-249/detail-en-light.png), [French/dark detail](images/mobile-249/detail-fr-dark.png) |
| Photo experience | [Immersive preview](images/mobile-249/photo-preview.png) |
| Shared interactions | [Confirmation](images/mobile-249/confirmation.png), [Modal](images/mobile-249/modal.png) |

## Validation record — 26 October 2026

Environment: Windows, .NET SDK 10.0.300, MAUI Android workload 10.0.0/10.0.100,
Android SDK 36/build-tools 36.0.0, Microsoft JDK 17.0.14. Emulator:
`emulator-5554`, `sdk_gphone64_x86_64`, Android 16/API 36, 1080×2400 at 420dpi.

Final results:

- `dotnet format FishingLogBook.sln --no-restore --verbosity quiet` — passed.
- `dotnet build FishingLogBook.sln --no-restore --verbosity minimal` initially
  failed because the shell's default Android SDK did not contain API 36. Re-running
  with the installed SDK and JDK paths passed with zero warnings and zero errors.
- `dotnet test FishingLogBook.sln --no-build --no-restore --verbosity minimal`
  with those paths — 3,158 passed (Mobile 75, migrations 10, Shared 18,
  Application 597, API 269, Infrastructure 289, Web 1,900).
- `npm test -- --reporter=dot` — 405 passed across 34 files.
- `npm run test:browser` — 159 passed and 7 expected WebKit service-worker tests
  skipped.
- Android ARM64 Release build with trimming and APK packaging — passed with zero
  warnings and zero errors. This proves compilation/package generation, not device
  behaviour for the Release artifact.
- Debug install on the Android 36 emulator — passed. All six ADB phases above passed:
  navigation/back/preview, persisted EN/FR and light/dark settings, all shared
  interactions, loading/empty/error/offline states, method filtering, and large-text
  narrow/landscape layouts. The actual native UI was visually inspected after each
  material polish change.

Observed Debug cold starts during ADB validation were approximately 3.0–3.5 seconds.
That is indicative emulator data, not a valid Release/physical-device measurement
against the 3-second budget. Memory and navigation latency were not instrumented.
Interactive styles retain a 48dp minimum target.

Search behaviour is covered by presentation tests for case-insensitive, accented,
method and cleared-filter paths. Automated ADB text injection was not reliable with
the emulator's stylus IME, so typing in the native search field remains a short manual
check. Physical-device screen-reader behaviour, tactile ergonomics and Release cold
start/memory remain unverified. Browser automation is intentionally limited to PWA
regression: it cannot exercise native MAUI controls, while the ADB runner exercises
the real Android controls and Shell boundaries.

## Self review

Self review:

- Issue/AC: PASS — every #249 acceptance criterion is implemented or explicitly
  identified above as a physical-device/manual measurement gap; no #250, auth, API,
  offline persistence or Catch-entry scope was introduced.
- Architecture/CQRS: PASS — changes remain inside native presentation, navigation,
  interaction and preference boundaries; no backend/CQRS path changed.
- Rules: PASS — repository workflow, C#, test, commit and GitHub rules were re-read.
- Security/privacy: PASS — fictitious samples only; no account, location, storage,
  upload or network privacy surface was added.
- Database/migrations: N/A — none changed.
- Tests/coverage: PASS — presentation transitions and failure/no-op paths have unit
  coverage, and real Android Shell/control boundaries have emulator coverage.
- Browser: PASS for regression — native E2E is handled by ADB; 159 browser tests
  passed for the unchanged PWA, with 7 documented engine capability skips.
- Web/UI/localisation: PASS — EN/FR resources have parity, user data is not translated,
  dark/light, large text, narrow layout and rotation were rendered on Android.
- Code smells: PASS — no feature-level framework or speculative abstraction was added;
  obsolete demo illustration and unused action artwork were removed.
- CI/deployment: PASS — mobile dependencies remain Android-targeted, the web-only
  solution remains MAUI-workload independent, and no deployment step is required.

Findings discovered during self-review:

1. **BLOCKER:** the typed popup used a `Shape` property that compiled in Debug but
   failed the trimmed Android Release build.
2. **SHOULD FIX:** the first polished header still gave Record Catch too much visual
   weight relative to the page title.
3. **SHOULD FIX:** clearing app data in the filter runner also removed Debug
   fast-deployment assemblies, making the validation itself unreliable.
4. **SHOULD FIX:** the large-text runner assumed one swipe would expose the preview
   action and failed at 200% text size.

Fixes made:

1. Moved rounded popup presentation to a supported inner `Border`; the trimmed
   Release APK now builds cleanly.
2. Replaced the oversized camera action with a compact 48dp text action, removed its
   heavy shadow, and changed the sample badge to quiet supporting copy.
3. Reset language/theme through the real Settings UI instead of clearing application
   data before filter validation.
4. Made large-text validation scroll until the real preview action is present and
   guaranteed restoration of device settings.

Remaining deliberate gaps:

- Search typing needs one manual emulator check because ADB text injection is not
  reliable with the configured stylus IME; all filtering logic is unit tested and the
  native method-filter path is exercised automatically.
- Physical-device screen reader, performance, memory and ergonomics checks remain.
- iOS is outside #249; this Android foundation does not claim iOS validation.

## Pre-merge maintainability review — PR #251

The review kept the accepted visual design and feature scope unchanged. Fictitious
Catch construction now lives in a singleton `SampleCatchProvider`; its DTO and
presentation instances are retained across filtering, state and culture refreshes.
Sample text uses explicit resource keys, while real recorded species names—including
names equal to an English sample value—remain untouched. Method filters and sample
method identities use an explicit enum instead of UI strings.

Catch presentation models now issue focused property-change notifications when the
culture changes. Detail and photo pages therefore no longer reset their complete
BindingContext during `OnAppearing`. Preferences have an explicit, idempotent startup
`Initialize` call before Shell is resolved instead of relying on an otherwise-unused
service resolution.

The native interaction presenter serialises dialogs and snackbar setup, replaces an
active snackbar before showing another, dismisses it before opening a dialog, and only
clears the snackbar instance that actually completed. All snackbar state mutation and
popup work remains on the MAUI main thread. Cancellation still propagates, closes an
open popup, and releases the dialog gate in `finally`.

Focused tests cover retained sample instances, typed method filtering, explicit sample
localisation versus real species text, property-change notifications, and one-time
preference initialization. Final validation after the review changes:

- `dotnet format FishingLogBook.sln --no-restore --verbosity quiet` — passed.
- Full solution build with the installed Android SDK/JDK — passed with zero warnings
  and zero errors.
- Full solution tests — 3,158 passed, including 75 mobile tests.
- Android ARM64 trimmed Release APK build — passed with zero warnings and zero errors.
- `npm test -- --reporter=dot` — 405 passed.
- `npm run test:browser` — 159 passed; 7 expected WebKit service-worker tests skipped.
- Updated Debug APK on Android 36: interaction and preference ADB phases passed. The
  interaction phase deliberately replaces six-second notifications at one-second
  intervals before opening confirmation/modal UI, exercising the overlap boundary.

Pre-merge self review:

- Issue/AC: PASS — all requested cleanup findings are addressed without visual or
  product-scope changes.
- Architecture/CQRS: PASS — one concrete feature-owned provider was added; no interface,
  repository or server dependency was introduced.
- Rules: PASS — naming, explicit enum values, DI registration, formatting and test
  structure follow repository conventions.
- Security/privacy: PASS — only fictitious local demo data changed; no identity,
  location, storage or network surface changed.
- Database/migrations: N/A.
- Tests/coverage: PASS — portable logic has focused unit coverage; MAUI-specific
  snackbar/popup concurrency is exercised through the installed Android UI.
- Browser: PASS — full PWA browser regression passed; native controls are not a
  Playwright boundary.
- Web/UI/localisation: PASS — appearance is unchanged; EN/FR switching and retained
  detail/preview bindings passed on Android.
- Code smells: PASS — repeated DTO construction, magic species comparisons, string
  filter state, BindingContext resets and implicit startup side effects were removed.
- CI/deployment: PASS — Android-only MAUI dependencies remain isolated; no migration,
  configuration or deployment action is required.

Remaining deliberate gap: CommunityToolkit snackbar/popup objects cannot be constructed
by the portable `net10.0` test target. Their UI-thread and overlap behaviour is therefore
covered by the Android emulator phase rather than a mock-only unit test.
