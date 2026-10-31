# Native Catch completion verification

Date: 31 October 2026. Dependency PR #280 was confirmed merged before creating
`feature/271-complete-native-catch` from the fetched `origin/main`.

## Confirmed defects and changes

- `PhotoCollectionEditorView` replaced its own inherited binding context. This
  invalidated the parent's `ViewModel="{Binding Photographs}"` binding, leaving
  blank camera/gallery controls. Its contents now bind to the dedicated photo
  ViewModel while the control retains the caller's context.
- Photo renumbering dropped metadata, and SQLite did not retain it. Renumbering,
  mapping, the additive version 2 migration, and reload now preserve metadata.
- Save validation could remain stuck after correcting inputs. Validation now
  reflects current fields and photo changes; the API's initial photo requirement
  remains enforced and explained before Save is available.
- Bait/lure and notes were absent from XAML. They now use the existing field
  styles and translations, and reload raises the notifications their bindings need.
- Saved-catch loading could replace an explicitly chosen photo location when
  photos disagreed. Loading preserves the saved fields; explicit photo-detail
  choices still apply private location proposals through the Catch ViewModel.
- A stale editor cannot save its contents under a different signed-in account.
- Successful Save returns to the refreshed local Catch list, with a local-save
  label. The detail view displays recorded bait rather than always claiming none.
- The photo editor now composes reusable carousel, details, and picker views.
  See [component boundaries](mobile-photo-components.md). It matches Web's active
  preview, arrows/swipes, counter, remove, metadata, chosen-details action, and
  expandable section. Browsing does not reorder or save photos. Existing ordering
  service actions remain tested and available to consumers.
- Catch actions use an app-themed bottom sheet. Tapping the main detail photo or
  a thumbnail opens the same existing-catch editor as the menu's Edit action.

## Android evidence

Emulator: `emulator-5554`, Android 16/API 36, x86_64, 1080x2400. Debug APK built
with the repository's Android SDK/JDK configuration, installed and exercised.
This is emulator evidence, not physical-device or release-build evidence.

Screenshots: [photo editor](evidence/271/photo-editor.png),
[saved catches in airplane mode](evidence/271/saved-catches.png), and
[French dark actions menu](evidence/271/actions-menu-fr-dark.png).

| Check | Result |
| --- | --- |
| Sign in with dedicated test account | Passed |
| Method/species defaults, chips, existing measurement editors | Passed; metric 1.5 kg and 42 cm entered and reloaded |
| Missing photo explains requirement and disables Save | Passed |
| Visible labelled camera/gallery buttons with working taps | Passed |
| Real JPEG selected through Android gallery | Passed |
| Multiple gallery selection | Passed; two selected files returned |
| Camera capture | Passed using emulator camera; third photo preview added |
| Camera cancellation and permission denial | Passed; cancellation retained photos; denial showed translated feedback |
| Large preview, arrows, swipe, counter | Passed |
| EXIF date and use-details action | Passed; 30/10/2026 15:06 displayed and applied to picker |
| Selected metadata updates when browsing | Passed; browsing alone did not change applied catch fields |
| Remove selected photo and protect last required photo | Passed; three photos reduced to two; sole saved photo removal disabled |
| Collapse/expand section retains editor state | Passed |
| EN/FR and light/dark photo layout | Passed; translated labels/counter and wrapping checked; delete icon contrast corrected |
| Optional bait/lure and notes, scrolling and keyboard | Passed; entered, saved, restarted, and reloaded |
| Private location capture | Passed; persisted visibility is Private |
| Offline Save and locally backed list | Passed with Wi-Fi/data disabled and with airplane mode enabled |
| Saved state and outbox inspected in SQLite | Passed; two Saved catches with one Pending intent each |
| Terminate/restart and reopen saved catches | Passed after system credential unlock |
| Explicit offline edit survives another restart | Passed; saved revision advanced from 12 to 13 with the edited notes |
| Leave edit without Save | Passed; saved revision, fields, and photo references unchanged |
| Independent new-catch draft restoration | Passed; draft notes restored without overwriting the saved catch |
| Durable photos and metadata after removing gallery source files | Passed; previews and EXIF date survived process restart and reload |
| Themed actions menu, cancel, menu Edit, photo/thumbnail edit entry | Passed |

The initial saved catch retained revision 13 while a separate draft was restored
and then saved as a second catch at revision 10. There were three persisted photo
rows across those catches. These revision numbers include earlier draft autosaves;
they are not API sync versions. No remote Catch synchronisation was implemented.

## Automated validation

- Mobile tests: 899 passed, including selected-photo navigation, no-op guards,
  metadata application, localisation, protected removal, cancellation/permission
  outcomes, partial selection, explicit Save, draft isolation, account isolation,
  notification regressions, real SQLite reload/edit/outbox semantics, and migration
  upgrade compatibility.
- Full solution build: passed with no warnings/errors; Android APK included.
- Full solution tests: 4,009 passed across seven projects.
- JavaScript tests: 409 passed.
- Browser regression tests: 159 passed, seven existing skips. One earlier run
  timed out opening the unchanged offline-shell landing page; the isolated retry
  and subsequent full run passed. No Web production code was changed.
- Formatting and diff whitespace checks: passed for changed C# and the final diff.

ViewModel tests do not prove native rendering. The Android checks above supply
that evidence. The Web Playwright suite cannot exercise native MAUI; no browser
host or test authentication was added to Mobile. A separate test-host project is
not warranted for this change.

## Self review

- Issue/AC: scoped native create/edit, local save, photo and draft scenarios verified.
- Architecture/CQRS: PASS; native MVVM, existing adapters/stores and shared rules;
  no server CQRS imposed on Mobile and no sync orchestration in views/ViewModels.
- Rules: PASS; feature folders, native controls, localisation, regression tests,
  branch provenance, and final validation reviewed against repository rules.
- Security/privacy: PASS; trusted account scope, stale-account save guard, private
  locations, owned durable paths; no credentials included in source or evidence.
- Database/migrations: PASS; additive nullable metadata column, existing rows
  preserved, real SQLite upgrade/read/write tests and device read-back.
- Tests/coverage: PASS for changed state transitions and dependency boundaries;
  platform behaviour additionally exercised on Android.
- Browser: PASS for existing Web regressions; native interactions use device checks.
- Web/UI/localisation: PASS for native EN/FR, light/dark, semantic action labels,
  touch targets, scroll/keyboard and the requested reusable composition.
- Code smells: PASS; three independent views, one photo selection owner, no new
  controller/coordinator or feature-specific replacement photo pipeline.
- CI/deployment: local validation passed; no deployment, cloud change, backend
  migration, Terraform apply, or automatic merge. SQLite upgrades locally on startup.

Findings discovered during self-review and fixed: invalidated photo binding,
discarded metadata, stuck validation, missing text fields/reload notifications,
saved-location mutation on load, stale-account saving, stale return destination,
partial-picker loss, dark delete-icon contrast, and narrow menu sizing.

Remaining deliberate gaps: independent remote sync/uploads (#272), trip association
and provenance (#273), and physical-device/iOS/release performance verification.
TalkBack was not exercised; accessible labels and actions were inspected in the
Android hierarchy. Existing Catch Remove/Share unavailable behaviour is unchanged;
this PR styles that menu and completes Add/Edit, not Delete/Share implementation.
