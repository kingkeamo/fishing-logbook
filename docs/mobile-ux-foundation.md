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
- The design dictionary owns the palette, text styles, spacing, cards, elevation,
  buttons, inputs and focus/pressed/disabled treatment. Text scales with Android;
  actions wrap and request at least 48dp. Scrollable layouts accommodate smaller
  screens. Tabs use native selection/accessibility behaviour.
- Catch cards/detail/preview lead with a bundled, explicitly labelled sample
  illustration. The original navy/teal/sky palette, launcher/splash and wordmark
  remain; the flyout uses the original wordmark without a permanent Catch banner.
  The Trips icon reuses the existing web FishingTrip SVG. Other small tab icons and
  the sample fish illustration are repository-owned vectors. System Roboto remains
  the existing font choice. Real user content is never translated.
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
user content, resource parity, and the full state-to-journal restoration sequence.

The ADB regression runner exercises the actual installed native app:

```powershell
node tools/mobile/validate-ux.cjs navigation
node tools/mobile/validate-ux.cjs preferences
node tools/mobile/validate-ux.cjs interactions
node tools/mobile/validate-ux.cjs states
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

## Validation record — 25 October 2026

Environment: Windows, .NET SDK 10.0.300, MAUI Android workload 10.0.0/10.0.100,
Android SDK 36/build-tools 36.0.0, Microsoft JDK 17.0.14. Emulator:
`emulator-5554`, `sdk_gphone64_x86_64`, Android 16/API 36, 1080×2400 at 420dpi.

Final command results and screenshot index are recorded below after validation.

## Self review

Final review/results are recorded below after validation.
