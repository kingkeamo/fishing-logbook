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
- Catch actions use an app-themed bottom sheet. Tapping the main detail photo
  opens the same existing-catch editor as the menu's Edit action. Thumbnails,
  previous/next controls and swipes only change the selected photo.
- The Catch detail page reuses `PhotoCarouselView` read-only. `CatchPresentationModel`
  owns one selected photo index (first photo on every open); thumbnails highlight
  the selected photo. Previous/next wrap, matching Web and the photo editor.

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
| Themed actions menu, cancel, menu Edit | Passed |

The initial saved catch retained revision 13 while a separate draft was restored
and then saved as a second catch at revision 10. There were three persisted photo
rows across those catches. These revision numbers include earlier draft autosaves;
they are not API sync versions. No remote Catch synchronisation was implemented.

### Catch detail photo interactions

Follow-up to the checks above. Same emulator (`emulator-5554`, Android 16/API 36,
1080x2400), Debug APK built from this branch and installed over the previous build,
signed-in account with two saved catches that each have two photographs. Hit targets
were read from the Android accessibility hierarchy (`uiautomator dump`) and each
interaction was followed by a fresh dump to confirm which page was showing.
Screenshot: [photo 2 selected](evidence/271/catch-detail-photo-selected.png).

Hit targets (px): main photo `ImageView` [42,221][1038,851]; Previous
[42,872][168,998]; counter label [179,911][765,960]; Next [776,872][902,998];
thumbnails [42,2026][231,2190] and [252,2026][441,2190]. No element overlays the
photo; the previous full-size `CardAction` button was removed. The only native
Edit entry points are the main photo and the menu's Edit action.

| Check | Result |
| --- | --- |
| Opens on Photo 1 of 2, first thumbnail highlighted | Passed |
| Tap counter text, species heading, info tile, gap under the photo, notes area | Passed; stayed on the detail page each time |
| Tap thumbnail 2, then thumbnail 1 | Passed; main photo, counter and highlight followed; no navigation |
| Next, Next, Previous, Previous | Passed; 2 of 2, wrapped to 1 of 2, wrapped to 2 of 2, 1 of 2; no navigation |
| Swipe left, swipe right, swipe left on the main photo | Passed; 2 of 2, 1 of 2, 2 of 2; no navigation |
| Tap the centre of the main photo | Passed; opened the Catch editor, system Back returned to the detail page |
| Tap the letterbox bars inside the photo frame | Opens Edit (the frame is the Image control); see gaps |
| Menu Edit | Passed; opened the Catch editor |
| Leave to the list and reopen the same catch after viewing photo 2 | Passed; reopened on Photo 1 of 2 |
| Single-photo catch hides previous/next | Not run; both saved catches have two photos and no data was edited to create one |
| Dark theme and French on the detail page | Not run on device; French copy covered by unit tests |
| TalkBack | Not run |

## Automated validation

- Mobile tests: 926 passed, including selected-photo navigation, no-op guards,
  detail-page photo selection/next/previous/wrap/single-photo/reset behaviour,
  XAML wiring that keeps Edit off thumbnails and carousel controls,
  metadata application, localisation, protected removal, cancellation/permission
  outcomes, partial selection, explicit Save, draft isolation, account isolation,
  notification regressions, real SQLite reload/edit/outbox semantics, and migration
  upgrade compatibility.
- Full solution build: passed with no warnings/errors; Android APK included.
- Full solution tests: 4,036 passed across seven projects.
- JavaScript tests: 409 passed.
- Browser regression tests: 159 passed, seven existing skips on the final full run.
  Two earlier full runs each had one different offline-shell test time out waiting
  for the unchanged public landing page (158 passed); the isolated retry and the
  following full run passed. No Web production code was changed.
- Formatting: `dotnet format` run on the solution. It also reordered `using`
  directives in seven files this change does not touch (including two Web
  files); those were reverted to keep the diff scoped, so that drift remains.

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

Detail photo follow-up self-review. Findings fixed: an Edit overlay button spanning
the photo card, thumbnails that opened Edit, no carousel state or navigation on the
detail page, and a now-unused `PhotoCounter` property (removed; the visible counter
is `PhotoPosition`). Two outdated assertions that required thumbnail-to-Edit were
replaced by tests that forbid it.

Photo ordering (reported, not changed): the editor offers no reordering control.
The per-photo Earlier/Later buttons were replaced by the carousel when the editor
was recomposed to match Web, and this PR's description records that carousel arrows
browse rather than reorder. `MoveEarlier`/`MoveLater` remain in the ViewModel and
service with tests, so stored order is preserved but cannot be changed from the UI.
Whether users need reordering is a product decision for a separate ticket.

Detail page observations: tapping the letterbox bars inside the 240dp photo frame
opens Edit, because the frame is a single Image control; a catch with one photo still
shows the "Photo 1 of 1" label (previous/next are hidden) because the carousel always
shows its position text, whereas Web hides the count for a single photo.

Remaining deliberate gaps: independent remote sync/uploads (#272), trip association
and provenance (#273), and physical-device/iOS/release performance verification.
TalkBack was not exercised; accessible labels and actions were inspected in the
Android hierarchy. Existing Catch Remove/Share unavailable behaviour is unchanged;
this PR styles that menu and completes Add/Edit, not Delete/Share implementation.
