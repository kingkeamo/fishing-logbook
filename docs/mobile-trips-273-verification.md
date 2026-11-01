# Native Trips parity and offline trip association (#273)

Verified on 31 October 2026 on `emulator-5554`, AVD `FishingLogbook`, Android API 36, against the
dev API with the supplied e2e test account. Credentials, tokens, signed URLs and coordinates are
excluded from this evidence. Web is the functional specification (`src/FishingLogBook.Web/Features/Trips`
and `RecordCatchEditor`); no Razor or MudBlazor was reused.

## What was built

| Web behaviour | Native implementation |
|---|---|
| Add Catch joins the active/requested trip; "Recording in …", leave, rejoin | `TripAssociationViewModel` (child of `CatchEditViewModel`, like `Photographs`) + `ITripAssociationService`; EN/FR strings |
| Angler selector for trip participants; recorder stays the signed-in user | `TripAssociationViewModel.AnglerOptions/SelectAngler`; only anglers the server listed (owner + accepted) are selectable |
| Trip no longer available → actionable save block | `ConfirmAvailableAsync` blocks a trip save with `CatchTripUnavailable`; "Remove from this trip" saves the catch alone |
| Draft keeps its trip; a draft without a trip stays standalone | `RestoreAsync`; an explicit requested trip overrides a draft's trip |
| Locally created unsynced trips, ordering/dependencies, terminal errors | `SqliteTripStore`, `TripMetadataSynchroniser`, `TripContributionSynchroniser`, `TripDependencyService`; catch sync waits (`WaitingForTrip`) or fails terminally (`TripUnavailable`) |
| Trip list, invitations accept/decline, start trip | `TripsPage` / `TripListViewModel` (Trips tab) |
| Trip page: facts, counts, timeline, notes, record catch, edit, finish, participants, invite/remove | `TripDetailPage` / `TripDetailViewModel`, `TripTimelineService` |
| Trip photographs | `TripDetailViewModel.AddPhotographAsync` through the existing photo editor service and durable file store |
| Edit a trip note (text and date/time, own notes only, within the trip window) | `TripDetailViewModel.BeginEditNote`; same upsert endpoint as add |
| Trip fishing location: saved-location chips, other location, clear | `TripDetailViewModel.LocationOptions/SelectLocation/ClearLocation` using the cached angler preferences and the shared `ChipView` |
| Add existing catches to a trip; remove a catch from a trip | `TripCatchService` re-saves the catch locally (offline-capable), the catch synchroniser waits for the trip; picker and remove in the edit panel |
| Edit a trip catch without losing its trip | `CatchWorkingCopyService` no longer rejects trip catches; trip and angler are preserved (other-angler catches stay blocked) |

New local migration `005_273_NativeTrips.sql` is additive (`localtripdocuments`, `localtripcontributions`).
No API, Terraform or backend migration change; the existing trip/participant/lookup endpoints were reused.
Contract fix: the participant client's `RemoveAsync` now returns `TripParticipantsDto` as the API does.

## Screens and components (mirrors Web)

Web screens: `ActiveTrip`, `TripEdit`, `TripList`. Mobile: `TripDetailPage` (trip view), `TripEditPage` (Update trip) and `TripsPage` (list).
Web components map to `Features/Trips/Components`: `ActiveTripBanner`, `ActiveTripView`, `TripCatches`, `TripEditor`, `TripLocationPicker`,
`TripNotes`, `TripPhotographs`, `TripTimeline` (plus the Catches page `TripAction` button). Notes, add-catches and participants are modals
(`Features/Trips/Modals`) opened through `ITripModalService` by the components that own the interaction; pages are constructor-injected and only
provide context, navigation and refresh. XAML-declared children cannot be constructor-injected on MAUI (checked on device: no parameterless
constructor), so the owning page assigns the service to the top-level component, which passes it down.

## Automated validation

- Mobile: 1,715 passed (was 1,301). Trip feature coverage 99.9% lines, 97.8% branches (37 testable files; pages, components and modals are device-verified). API 289, Application 597, Infrastructure 291, Shared 56, migrations 10, Web 1,869.
- JavaScript 409 passed; browser suite 159 passed, 7 existing skips.
- New coverage: HTTP contracts (trip, participant, lookup clients, incl. exact URI/verb/body); real-SQLite trip service,
  association service, dependency gate, trip and contribution synchronisers (incl. an edit made during sync stays pending;
  URL → bytes → record order); list/detail ViewModels (permissions, notes, finish, edit, participants, invite search,
  photographs); editor trip/angler/unavailable/draft flows; catch synchroniser trip dependency; navigation parameters.

## Emulator evidence

| Check | Result | Evidence |
|---|---|---|
| Trips tab: empty state, Start fishing, trip list | Passed | [Trip list](evidence/273/trip-list.png) |
| Start a trip offline-capable and it reaches the server | Passed | Pending label cleared; participants call on the new trip succeeded |
| Trip page, timeline, add note | Passed | [Trip with catch and note](evidence/273/trip-detail-with-catch-and-note.png) |
| Record catch from the trip: "Recording in …", leave / add back | Passed | Observed on device |
| Save catch in trip, returns to the trip, syncs, appears on the server timeline | Passed | Catch showed "Synced." and "1 catch recorded" from the server detail |
| Offline: association resolves, save, force-stop/reopen, pending survives | Passed | Airplane mode; "Waiting to sync." after restart with offline notice |
| Reconnect: dependency-ordered sync | Passed | Pending cleared; trip lists both catches |
| Finish trip | Passed | Status "Finished", Finish/Update/Record hidden, Finished timeline entry |
| Participants panel and invite search (real server) | Passed | Owner shown; invited e2e account shown as "Invitation pending" |
| Add trip photograph (gallery) → upload → register | Passed | [Trip photograph](evidence/273/trip-detail-with-photograph.png) |
| Edit a note on device | Passed | Text changed in place, still one note |
| Add a note with date/time pickers | Passed | Pickers shown, validation text shown for out-of-window times (unit tested) |
| Remove a catch from the trip (edit panel) | Passed | Confirmation, then "No catches yet"; catch stays in the logbook |
| Add existing catch picker, then add it back | Passed | Candidate listed, "Add 1 catch", trip shows it again, synced after reopen |
| Trip location chips and Save | Passed | Saved locations shown, chip fills the entry, Save returned to the trip with the new place |
| Modals from components (note add/edit, participants, add catches) | Passed | Opened from the floating button, timeline icon, editor header and Add catch |
| Banner and Update trip button on Catches | Passed | Appear after sign-in without leaving the page |
| Accept / decline an invitation as the invited user | Not run | Needs signing in as the second account |
| Angler selector with an accepted participant | Not run | Needs an accepted participant; covered by automated tests |
| Camera capture for a trip photograph | Not run | Gallery path only; camera shares the same service |
| Trip catch edit of a server-backed trip catch on device | Not run | Covered by working-copy and editor tests |
| French layout / larger fonts | Not run | EN/FR strings added; only English layout inspected |
| Physical device | Not run | Emulator only |

## Findings during the work (fixed)

1. The working-copy store did not create the `localtrips` stub row required by the catch foreign key, so a trip catch could not be edited.
2. Saving a catch recorded from a trip left the editor on the Trips stack; it now returns to the trip.
3. Trip date in the editor used the device culture; it now uses the app culture.
4. The participant client's remove contract did not match the API.
5. The catch synchroniser's trip dependency gate had no direct tests.

## Self review

```text
Self review:
- Issue/AC: PASS for the AC; see gaps for Web features not yet ported
- Architecture/CQRS: PASS (MAUI MVVM; child ViewModel, no presenter/controller; existing API reused)
- Rules: PASS (EN/FR strings, 48dp actions, AutomationIds, feature folders)
- Security/privacy: PASS (user id from the session; angler selection limited to server-listed participants;
  invite/remove owner-only in the ViewModel and server-enforced; no coordinates added)
- Database/migrations: PASS (additive SQLite migration 005; account-scoped; no backend migration)
- Tests/coverage: PASS (see above); camera and accept/decline are device-unverified
- Browser: N/A (native MAUI; emulator evidence per maui.md)
- Web/UI/localisation: PASS for English layout; French not inspected
- Code smells: PASS
- CI/deployment: No infrastructure change; no manual post-merge step
```

## Web parity status

Every Web trip capability is now ported except the ones listed here.

- Other-angler catch editing stays blocked until provenance and location privacy can be preserved (also blocked on Web for catches recorded by another angler).
