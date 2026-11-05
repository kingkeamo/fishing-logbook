# Dashboard UX refinements — #22 / PR #301

## Behaviour

Native filters start collapsed. The summary and active-filter indication describe
the last successful application, independently of draft picker selections.
Apply submits an immutable selection, updates results and collapses on success.
Invalid or failed submissions leave the panel expanded and retain the prior applied
summary. Refresh/retry and page reappearance use the applied selection. Reset
submits This year with no species, method or venue restriction.

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
dates display the existing localized Not recorded text. Native rows stack species,
method and date alongside the prominent weight. Web headers and values have extra
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
