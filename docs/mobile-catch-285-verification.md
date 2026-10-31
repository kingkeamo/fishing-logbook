# Native server-authoritative history verification (#285)

Verified on 31 October 2026 on `emulator-5554`, AVD `FishingLogbook`, Android API 36.
The user confirmed this is the requested emulator. No physical device was connected.
Credentials, tokens, signed photo URLs and coordinates are excluded from this evidence.

## Automated validation

- Mobile: 1,301 passed, including 78 tests added during this review.
- API: 289 passed; other .NET suites: Shared 56, Application 597, Infrastructure 291,
  migrations 10, Web 1,869. Combined with Mobile: 4,413 passed.
- Full solution build and embedded Android APK build: zero warnings/errors.
- JavaScript: 409 passed. Existing browser suite: 159 passed, seven existing skips.
- `dotnet format` run; `git diff --check` passed.

New coverage exercises authenticated GET contracts, anonymous photograph download, server/local
GUID merge and precedence, offline/401/503 handling, stale account responses, working-copy photo
identity and bytes, interrupted-download recovery, account isolation, cleanup eligibility and
transactional revision rechecks, active-editor races, restart recovery and deletion retry.
Location tests cover capture progress, repeated taps, failure/retry, preserved place/source/
visibility, and adding photos without replacing the saved location.

## Emulator and dev API evidence

| Check | Result | Evidence |
|---|---|---|
| Online remote history and three-photo detail | Passed | [Remote detail](evidence/285/remote-detail.png); current server record opens after its local saved row has been removed |
| Remote-only Edit creates a durable working copy | Passed | Catch `ae44bca5-939c-49f5-bcdd-50a23b8f6027` hydrated at revision 1 with its two original photo IDs |
| Offline edit, explicit Save, force-stop/reopen | Passed | Revision 2 retained `285 emulator offline edit`; [offline pending list](evidence/285/offline-pending.png) shows pending data and hides confirmed history |
| Reconnect and authoritative reread | Passed | Independent authenticated GET confirmed the edited notes; three server catches, seven downloadable non-empty photographs |
| Cleanup outside the list and across restart | Passed | Saved rows and pending file-deletion queue became empty; both existing account-scoped drafts remained |
| Location button hit target and feedback | Passed | [Pin, wrapping label and right-aligned action](evidence/285/location-row.png); progress and an inline failure/retry were exercised |
| Fresh location capture and offline persistence | Passed | Android synthetic GPS/network test providers supplied a fix; explicit offline Save stored `DeviceGps`, `Private` and non-null coordinates for catch `66285fdf-4733-4be4-86ff-0dd8ab22840d` |
| Location restart/sync/server persistence | Passed | Record survived force-stop; subsequent independent GET confirmed private `DeviceGps` location and all three durable photographs; native remote detail shows Private location after local cleanup |
| Runtime permission declarations/state | Passed | Coarse/fine permissions declared in source and generated manifest; emulator already had foreground permission. No repeat prompt is expected in that state |
| Physical-device GPS and fresh install permission prompt | Not run | No physical device connected; emulator permission was already granted |
| Create a new catch using deployed Web UI, then load native | Not completed | A fresh Web sign-in reached the deployed error boundary. Retrying the history route did not yield a usable Web session. No Web production code or auth settings were changed |

The independent server check used the existing native public-client Authorization Code + PKCE
flow for the supplied test account, then GET `/api/catches` and each presigned image. It did not
reuse SQLite or the native list as proof. The synthetic test providers were removed afterwards;
device location, Wi-Fi and data were restored, and airplane mode disabled.

## Self review

- Issue/AC: native history, editing, cleanup and location behaviour validated; the deployed
  Web-create-to-native journey remains unverified as described above. Do not close #285 yet.
- Architecture/CQRS: PASS. Existing API/shared DTOs and MAUI MVVM boundaries retained; no new
  presenter/controller, backend contract or sync orchestration in the editor.
- Rules: PASS. Scoped native changes, localized EN/FR text, existing styles/icons and 48dp action.
- Security/privacy: PASS. Account-scoped stores, stale-account guards, owned photo paths,
  server exposure respected, private capture default; evidence excludes coordinates/secrets.
- Database/migrations: PASS. Existing additive SQLite migration 004; no backend migration.
  File-reference checks/deletion are serialized with writes; interrupted hydration is queued.
- Tests/coverage: PASS for the tested native and stateful boundaries; live account switching,
  terminating exactly during the delete transaction, and a fresh-install permission prompt were
  not exercised manually. Real SQLite race/restart tests compensate for those state transitions.
- Browser: existing suite PASS. Native bindings use emulator evidence; no Playwright tests
  were added to Mobile. Deployed Web authentication failure is recorded separately.
- Web/UI/localisation: native EN/FR resources present; actual English phone layout inspected.
  French capture messages and larger font layouts have automated/resource coverage, not a full
  manual device pass.
- Code smells: PASS. Selection/navigation not duplicated; active-edit protection extended at
  the existing registry; storage filenames differ from photograph identity deliberately.
- CI/deployment: local checks passed. No infrastructure change/apply and no auto-merge.

Findings discovered and fixed:

1. Cleanup editor/file-reference checks raced deletion. Gate editor acquisition and check file
   references in the SQLite transaction that performs deletion/queue completion.
2. Server checks only compared photograph IDs. Compare catch metadata and owned location too;
   allow only PostgreSQL microsecond timestamp precision rounding.
3. Interrupted hydration could strand files. Queue unique owned storage paths before writing;
   attach references and cancel deletion markers atomically.
4. Confirmed historical copies appeared offline. Exclude them from the local overlay while
   retaining their durable bytes until cleanup is eligible.
5. Remote location display required a place name, and saved photo locations could be replaced
   when another image was added. Preserve location/visibility and display its safe description.
6. Capture looked inactive. Show local progress/result/retry, prevent repeated requests and
   prevent saving while capture is pending; align the action with the pin and caption.

Remaining deliberate gaps:

- TODO #273: trip and other-angler provenance editing, as requested by the user. Unsupported
  records are blocked rather than saved with altered attribution.
- Resolve/reverify the deployed Web sign-in/create journey before treating #285 as complete.
- Legacy unreferenced files without a deletion marker are retained conservatively. The new
  recovery protocol covers tracked cleanup and working-copy preparation, not indiscriminate
  deletion of unknown files from earlier builds.
- Existing unpaginated API and one-hour photo URL lifetime remain unchanged.

## Location error presentation correction

The same location failure was being assigned to both the location status and the form-wide
status, and the inline label inherited muted body text. Location capture now updates only its
own status. Its derived error state drives the existing light/dark danger colours; progress
retains its normal styling. Successful capture updates the existing location row and hides the
status label. Form save status remains independent.

Final Mobile validation: 1,301 passed; Android build: zero warnings/errors; scoped formatting
verification and `git diff --check` passed. Tests cover each failure without a duplicate form
status, non-error progress/success, retry, and error-state change notifications.

On emulator-5554, resetting location permission displayed the native Android permission prompt.
A denied-permission case then showed exactly one error node beside the location action, with
no duplicate above Save. Actual rendering was inspected in
[light mode](evidence/285/location-error-light.png) and
[dark mode](evidence/285/location-error-dark.png). Permissions were restored to granted and
night mode restored to off after the check. No catch edit was saved during this presentation test.

Self review: targeted behaviour, MAUI ownership/bindings, existing theme/localisation conventions,
accessibility text and regression tests passed. Persistence, API and infrastructure are unchanged
by this correction; the wider #285 validation gaps above remain unchanged.

## Single privacy caption after capture

Removed the additional successful-capture message and its unused EN/FR resources. After capture,
privacy appears only in the existing pin/caption row, with the Update location action; progress
and red failures still use the inline status when needed. No save or privacy policy changed.

Final validation: 1,301 Mobile tests passed, Android build had zero warnings/errors, scoped
formatting verification and `git diff --check` passed. Emulator-5554 received a synthetic test
fix; UI inspection confirmed exactly one privacy caption, zero extra status labels and the
Update location action. The temporary test providers were removed afterwards. No catch was
saved during this presentation check, and coordinates were excluded from the evidence.

Self review: requested copy/visibility behaviour, MVVM notifications, EN/FR resource consistency,
failure/retry preservation and native rendering passed. API, persistence and infrastructure
were unchanged; the previously documented wider #285 validation gaps remain.
