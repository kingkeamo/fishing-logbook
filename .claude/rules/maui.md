---
paths:
  - "src/FishingLogBook.Mobile/**/*.cs"
  - "src/FishingLogBook.Mobile/**/*.xaml"
  - "tests/FishingLogBook.Mobile.Tests/**/*.cs"
---

# .NET MAUI conventions (`FishingLogBook.Mobile`)

Architecture source: **ADR 0001** (`docs/adr/0001-native-mobile-frontend.md`).
General C# style (comments, braces, complexity, naming) is **`csharp.md`**.
Automated test shape is **`testing-csharp.md`**. Device evidence is this file.

Do **not** apply server CQRS, `IMediator`, Application/Infrastructure repositories,
or Blazor/MudBlazor/Playwright rules to Mobile.

## Host and renderer

- Target **.NET 10 MAUI** with native XAML and C#. The `net10.0` TFM exists so the
  same types can be unit-tested without a device.
- **No** `BlazorWebView`, Razor UI, HTML/CSS wrappers, or WebView substitutions in
  the Catch (or any core) journey.
- Web is a functional and visual **reference only**. Do not extract MudBlazor or
  Razor markup for reuse.
- Reuse portable **Shared** contracts, validation, conversion and domain logic
  (`IMeasurementService`, `ICatchPhotographProposalService`,
  `PhotographMetadataProcessor`, DTOs/enums/constants).
- Mobile must **not** reference `FishingLogBook.Application` or
  `FishingLogBook.Infrastructure`.
- Prefer an existing native control over creating a duplicate.

## MVVM

| Owner | Responsibility |
|---|---|
| XAML | Layout, styles, visual states, bindings, `AutomationId`, semantics |
| ViewModel | User-facing state, actions, validation, lifecycle transitions |
| Code-behind | Thin MAUI glue: focus, native events, popup show/close, navigation |
| Platform adapters | Camera, gallery, permissions, location, secure storage, file system |
| Stores | SQLite and durable photo files |

- Bind the page or control `BindingContext` to the ViewModel (or a dedicated
  `ViewModel` bindable property that **is** the BindingContext — see
  `PhotoCollectionEditorView`).
- ViewModels implement `INotifyPropertyChanged`. Raise change notifications for
  every bound property that can change, including `CanSave` / enabled states when
  validation or collections change.
- Public ViewModel methods are the actions. Code-behind `Clicked` / `Tapped`
  handlers call those methods. Do **not** introduce `ICommand` / MVVM toolkits
  unless an existing control already requires them.
- Code-behind must not own catch/draft/save business rules. The ViewModel decides
  `ChooseMethod` / `CompleteMethodPicker`, `CreateWeightEditor` /
  `CompleteWeightEdit`, `SaveAsync`, `OnDisappearingAsync`.
- Platform and persistence interfaces are substituted in tests. Do not wrap a
  ViewModel in a new layer to make it testable.

**Forbidden for feature screens:** new `Presenter`, `Session`, `Coordinator`, or
`Controller` types whose only purpose is to sit between XAML and a ViewModel.

Keep the existing authentication/startup types
(`AccountSessionCoordinator`, `SessionLifecycleCoordinator`, `StartupCoordinator`,
`IInteractionPresenter` / `NativeInteractionPresenter`). Do not copy that pattern
onto Catch, measurements, photos, or other feature editors.

One owner per piece of state. Do not keep a parallel editor/session copy of
ViewModel fields.

## Folders and names

Feature-first, same rule as Web: if one feature owns it, it lives under that feature.

```text
Features/<Feature>/
    Pages/              ContentPage
    Components/<Name>/  feature-owned composite views (XAML + code-behind)
    ViewModels/         *ViewModel
    Models/             *Model
    Services/           *Service / I*Service
    Clients/            *Client / I*Client
    Offline/Stores/     SQLite *Store
    Storage/Stores/     file *Store
    Mappers/            *Mapper
    Popups/             CommunityToolkit popups
    Views/              feature-owned reusable views (e.g. photographs)

Native/Controls/        cross-feature controls (ChipView, MeasurementEditorView)
Native/Adapters/        MAUI/device implementations of IO ports
IO/Adapters/            I*Adapter ports
Platforms/<os>/         OS-specific registrations and implementations
Localization/           UiTextService + UiStrings.resx / UiStrings.fr.resx
```

- Namespaces match folders. One production type per file. File name matches type name.
- Cross-feature UI belongs in `Native/Controls/`. Feature-specific UI stays in
  `Features/<Feature>/`.
- Do not create `Helpers/`, `Utils/`, `Managers/`, `Presenters/`, or `Sessions/`.
- Pages and views keep natural UI names (`CatchEditPage`, `ChipView`). Role suffixes
  stay on ViewModels, services, stores, adapters, mappers.
- Register new services in `MauiProgram.cs` or the platform `MauiProgram.*.cs`
  partial, not only at the call site.

Before adding a file: owning feature, role, folder, name. Read at least five
existing files of the same kind.

## Native controls

Compose pages from existing controls and the design dictionary
(`Resources/Styles/DesignSystem.xaml`):

- `ChipView` for method/species/angler chips (`Tapped` + `CommandParameter`)
- `MeasurementEditorView` + `MeasurementEditorPopup` for weight/length
- `PhotoCollectionEditorView` for camera/gallery/order/remove
- `CataloguePickerPopup` / `CataloguePickerViewModel` for MORE…
- `ContentStateView`, `IconView`, shared button/field styles

Do not add a second control that duplicates an existing purpose (second chip,
second measurement editor, second photo strip, second catalogue picker).

Preserve BindingContext, `INotifyPropertyChanged`, and page/popup lifecycle.
`CatchEditPage` initialises the ViewModel **once** (`OnAppearing` + `_initialized`).
Opening a popup must not re-run `InitializeAsync`.

Interactive controls need visible labels **and** working taps. `AutomationId` is
required on primary actions and fields. Empty or overlapping label text is a
defect, not a styling footnote.

## Interaction correctness

Native handlers, focus, and keyboard are part of the product, not view details.

- One **canonical** measurement value in the ViewModel. Entries and the slider
  display that value; they do not each own a second source of truth.
- Pending Entry text commits only when the user actually edited the Entry.
  A later slider / ± / Clear value must not be overwritten by stale focused text
  on Apply or Unfocused.
- Force Entry text through the MAUI handler when the ViewModel changes the value
  while an Entry may still be focused (`Handler.UpdateValue`).
- Suppress Entry commit while the ViewModel is pushing slider/±/Clear updates.
  Mark the Entry edited only on real `TextChanged` outside that sync.
- Do not `Unfocus` immediately before Apply in a way that looks like a new edit.
- Button `IsEnabled` / `CanSave` must update when photos, validation, or conflicts
  change.
- Date/time pickers show **local wall-clock**. Persist a UTC instant using
  `TimeProvider.LocalTimeZone` (same semantics as `DevicePhotographLocalTimeMapper`
  and Web `datetime-local`). Do not `SpecifyKind(..., DateTimeKind.Utc)` on a
  local picker value.
- Support light/dark (`AppThemeBinding`), EN/FR via `UiTextService` (add both
  `.resx` values; no hard-coded English), accessibility descriptions, and
  practical phone layouts (48dp actions, wrapping, scroll).

## Offline-first data

- User **Save** writes account-scoped SQLite through the existing `ICatchStore`
  and does **not** wait for network or API.
- Durable photos go through `IPhotographFileStore` (relative paths, temp-then-move).
- **New catches** debounce-autosave as `Draft` and restore the latest draft on
  reopen / restart.
- **Existing saved catches** change only on explicit Save. Back, popup close, and
  `OnDisappearing` must not persist edits and must never demote `Saved` to `Draft`.
- After a successful Save, do not write a subsequent draft.
- Local Save and remote synchronisation are separate. ViewModels and UI must
  **not** orchestrate sync. Outbox rows stay consistent with the store invariants
  in `docs/mobile-sqlite-persistence.md`.
- Isolate data by signed-in user. Account switch must not show another user's
  catches or photographs.

## Photographs

- Use `PhotoCollectionEditorView` + `PhotographCollectionEditorViewModel` +
  `IPhotographCollectionEditorService`. Camera/gallery, permissions, previews,
  ordering, removal and durable bytes belong there.
- Feature code supplies requirements (minimum photo count, last-photo guard) and
  applies metadata proposals (`ICatchPhotographProposalService`).
- Bind `ViewModel="{Binding Photographs}"` (or the equivalent bindable). Do not
  leave the editor on the page BindingContext by accident.
- Never replace real photo capture/storage with placeholders or dummy images.
- Do not delete durable files that still have a SQLite reference, including
  unsynchronised rows. Orphan cleanup must not remove prepared files that may
  still be attached to a draft.

## Testing and verification

Automated tests live in `tests/FishingLogBook.Mobile.Tests` and follow
**`testing-csharp.md`** (`WhenTesting`, NSubstitute `Mock*` fields, Arrange / Act /
Assert, `Received(n)` / `DidNotReceive()`).

- Test the **production** ViewModel and real state transitions (save, draft,
  leave-without-save, validation, cancel, photo proposals, units).
- Substitute platform adapters and store interfaces. Use the real Shared
  measurement/proposal services unless the test is isolating a different boundary.
- Cover success, validation, cancellation, failure, and lifecycle
  (appear / disappear / popup / restore).
- Build `net10.0` and run `dotnet test tests/FishingLogBook.Mobile.Tests`.

**Green ViewModel tests are not native proof.** For UI, binding, focus, keyboard,
popup, photo-picker, or navigation changes:

1. Build the `net10.0-android` target.
2. Install and exercise the flow on an emulator or device when one is available
   (`ANDROID_HOME` / `adb` — do not claim they are missing without checking).
3. Report each device check as **passed**, **failed**, or **not run**.
4. A failed device check is a defect. An unrun check is unverified, not a pass.

Do not treat missing emulator access as a product blocker for a future PR
(sync, trips). Respect the issue's Out of Scope. Do not start #272 or other
follow-on work from a Catch UI ticket.

Playwright / bUnit cannot validate native MAUI. Do not add them to Mobile.

## Before declaring Mobile work complete

1. Read the issue acceptance criteria.
2. Automated Mobile tests pass for the changed behaviour.
3. Self-review (`self-review.md`) includes device evidence or an explicit
   unverified list.
4. Do not call the PR ready on test counts alone.
