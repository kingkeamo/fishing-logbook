# Dashboard UX refinements — #22 / PR #301

## Behaviour

Native filters open in a centered modal using the Trips feature pattern. A modal
service obtains a fresh transient filter ViewModel through a factory for each
opening. That ViewModel owns its draft, validation, localized choices and immutable
Apply/Reset result. It never mutates the Dashboard or calls the API. Close, outside
tap and Android Back return no changes. The Dashboard's applied summary remains
unchanged while editing, and only a returned selection triggers a server refresh.
API failures stay distinct from empty results; Retry repeats the submitted request
without claiming it was applied. The summary updates after successful loading.
Opening or returning from the modal does not reload the Dashboard.

The Web toolbar places the four primary filters and compact actions together when
space permits, wraps on phones, and places custom inclusive dates on a separate row.
Both clients render three additional Explore cards from the API's `Filtered` totals.
Only matching catches' persisted `TripId` associations contribute to filtered trips;
null associations do not count and duplicate associations count once. Missing
species are excluded from distinct species. Weightless catches still count.
Lifetime aggregation is unchanged, including recorded trips without catches.

Both PB sections retain the winning catch ID and canonical weight and now include
nullable `CaughtOn`. Ties still use weight descending, catch instant ascending and
UUID ascending. Dates use the browser/device local timezone and UI culture; no
separate date-format preference exists in the current profile contract. Missing
dates display the existing localized Not recorded text. Native PB sections each
remain one card with a compact row per species/method combination: bold species
and weight share the first row, followed by method and date, with subtle dividers.
Redundant column headers are removed. Web headers and values have extra
separation between weight and date, with narrower padding on phones.

The native Dashboard Record Catch button and both redundant footers are removed.
The Web Record Catch action, native Catches recording journey, bottom navigation,
contextual empty states and first-time Import suggestion remain available.

## Validation on 5 November 2026

- Full solution build: passed with zero warnings/errors, configured Android SDK/JDK,
  `--disable-build-servers -m:1` and bundled Debug APK assemblies.
- Full solution tests: 5,671 passed. Includes Shared 93, Application 754,
  Infrastructure 342, migrations 10, API 321, Web 1,994 and Mobile 2,157.
- PostgreSQL/Testcontainers tests cover the real SQL result-set order, filtered
  totals, null/deduplicated trip associations, user isolation, all filter dimensions,
  empty/weightless/unclassified catches, PB dates and deterministic ties.
- JavaScript: 409 passed with one worker.
- Authenticated Chromium Dashboard E2E: two passed against real Cognito, Web, API
  and a disposable migrated database. Checks filtered cards/dates, inclusive filters,
  unchanged lifetime data, desktop toolbar alignment, winning-catch navigation and
  failure/retry. Desktop/mobile and light/dark screenshots were generated locally.
- Existing browser regression suite: 158 passed, seven configured skips and one
  cold-offline-startup timeout. That unchanged Chromium scenario passed on targeted
  rerun. The initial high-concurrency run was stopped after worker exhaustion.
- Final Web PB spacing change: all 1,994 Web tests passed again. The additional
  padding is shared by both PB tables and their headers; mobile padding is reduced.
- Formatting completed; unrelated pre-existing formatter changes were excluded.
  Diff whitespace checks passed.
- Android: final Local Debug APK built, installed and launched. Collapsed filters,
  applied default summary, filtered-card labels and removal of the native Record
  Catch action were observed. Authenticated Apply/Reset, populated PB date layout,
  narrow-phone interaction and French runtime checks remain with the user, who
  requested exclusive use of the emulator during testing. ViewModel and XAML
  presentation tests cover the corresponding state and binding contracts.

## Self review

- Issue/AC: implementation covers the requested refinements; remaining native
  manual checks are explicitly listed above rather than claimed as passed.
- Architecture/CQRS: PASS. The existing endpoint → mediator → handler → service →
  repository path and injected Mapster mappings remain intact. Aggregation is SQL.
- Rules: PASS. Feature-owned MVVM/Razor components, existing design resources and
  EN/FR localization; no replacement navigation, UI framework or client aggregation.
- Security/privacy: PASS. Authenticated angler ownership remains server-derived;
  queries are parameterized, snapshots remain repeatable-read, responses remain
  no-store, and stale/account-exit responses are guarded. No credentials committed.
- Database/migrations: PASS. Existing `catches.tripid` foreign key and catch instant
  are read directly. No schema migration or data modification is needed.
- Tests/coverage: PASS for automated checks with the browser timeout/rerun recorded.
  Tests exercise real persistence and HTTP/application mapping in the appropriate
  separate test projects, plus both clients' error/empty/formatting behaviour.
- Browser: authenticated Chromium covers the Dashboard journey; existing regression
  harness covers IndexedDB/service workers. Dashboard-specific WebKit and installed
  PWA rendering remain unverified; no new testing infrastructure is required here.
- Web/UI/localisation: PASS in automated EN/FR scenarios and Chromium screenshots;
  remaining native device checks are listed above.
- Code smells: reviewed immutable selection ownership, request races, cancellation,
  result-set ordering and shared table styling. No unrelated refactor was included.
- CI/deployment: existing workflows remain unchanged. Deploy the matching API and
  clients together through the existing process. No Terraform apply, migration,
  cloud deployment, signing change, issue closure or PR merge was performed.

Findings discovered during self-review and fixes made:

1. Draft selections could be mistaken for active filters on refresh. Separate the
   submitted/applied immutable selection and commit it only after successful loading.
2. Failed loads could discard draft picker values. Preserve draft selections and
   choices on ordinary failures while clearing account-scoped choices on account exit.
3. Custom dates could force primary desktop filters onto separate rows. Place custom
   dates beneath the compact toolbar; verify primary control alignment in Chromium.
4. New French strings lost accents through shell encoding. Correct the resource
   values and retain EN/FR summary and card/date regression assertions.
5. Asynchronous browser-local PB dates needed lifetime cancellation and response
   guards. Cancel conversion when the component is disposed and ignore stale results.
6. Testing feedback found weight and date columns too close together. Add shared
   header/cell padding, with compact mobile overrides, and rerun all Web tests.

Remaining deliberate gaps:

- User-owned native emulator checks listed above; no iOS runtime claim.
- One existing cold-offline browser timeout passed on targeted rerun; no unrelated
  offline-startup production or test changes were made for this task.
- Charts, length rankings, badges, sharing and offline analytics remain out of scope.

## Native modal and PB follow-up

The user approved the compact PB rows and replaced inline expand/collapse with a
modal. They specifically requested the Trips architecture rather than a popup
bound directly to the long-lived Dashboard ViewModel. The final implementation is:

- DashboardFilterModal in Features/Dashboard/Modals with thin native event handlers.
- IDashboardModalService for testable opening/result orchestration.
- DashboardModalViewModelFactory and DI registration creating a transient
  DashboardFilterViewModel for every opening, following Trips.
- An independent draft initialized from the applied selection and server choices.
- Dashboard-owned API loading after Apply/Reset, with cancellation/account guards.
- One PB card per section, with compact species/method records and no column headers.

Self review findings and fixes:

1. Popup lifecycle events could cancel or reload the Dashboard. Keep its appearance
   lifetime while the modal is open and skip duplicate appearance loading.
2. A shared Dashboard binding would couple draft edits to the long-lived screen.
   Use the Trips modal service/factory and independently tested transient editor.
3. Reopening must restore custom dates and preserve applied classifications even
   when a choice is no longer returned by the API. Initialize fresh drafts from the
   immutable applied selection and retain missing selected options explicitly.
4. Late API/preference responses or modal results after cancellation/account exit
   must not overwrite current data. Check cancellation after dependency awaits,
   preserve the previous successful snapshot on cancellation, and discard stale
   modal results. Failed server requests are retained for an accurate Retry.
5. Toolkit cancellation only stops waiting for a result; it does not close a modal.
   The native modal service explicitly requests dismissal, including cancellation
   during modal opening, and disposes that registration on completion.
6. The close icon's black source would be unclear in dark mode. Use the existing
   theme-aware icon tint. Full-width fixed footer actions accommodate French labels.

Automated validation and native runtime evidence are recorded in the PR. The test
coverage includes factory registration and distinct instances, draft isolation,
invalid/custom dates, snapshot immutability, EN/FR choices, reset, dismissal without
API calls, duplicate-open guards, failed-request retry, account changes, late API
and preferred-unit responses, existing winning-catch navigation, and XAML contracts.
No browser tests are added for MAUI; actual modal rendering, native pickers, Back
and outside dismissal, scroll reachability and safe areas require emulator checks.
