# Mobile Catch and photograph synchronisation: verification (#272)

Design and flow: [mobile-catch-sync.md](mobile-catch-sync.md). Scope: Catch metadata sync,
photograph upload sync, durable retries and recovery, persisted status and manual retry.
Out of scope: #273 trips, #274 hardening, #282, #283, publishing.

## Acceptance criteria to evidence

| Acceptance criterion | Automated evidence | Device evidence |
| --- | --- | --- |
| No duplicate catches on retry | `CatchMetadataSynchroniserTests` lost-response and restart cases assert one server record against a stateful stand-in; `CatchRepositoryTests.WhenTestingUpsert` proves the real server upsert keeps one catch row and one set of photograph rows when the same id is posted twice | Re-sync of an edited catch produced one extra completed operation and no new photograph operations |
| Interrupted sync recovers | Lease-expiry, previous-process lease, crash-after-send, cancellation and connectivity-drop cases in `OutboxSyncCoordinatorTests`, `SqliteOutboxStoreTests`, both synchroniser suites | Network cut during a photograph upload; all three operations `Pending` with a backoff; after reconnecting all three completed on attempt 2 |
| Newer local edits are never overwritten by a stale response | `CatchMetadataSynchroniserTests`: an edit saved inside the in-flight request keeps its notes and revision, the acknowledged revision is the one sent, a new `Pending` operation remains and is sent next | Offline edit (revision 14 to 15) synced as revision 15 |
| Photograph failures retain local data | Photograph suite asserts `DeleteFile` is never called, rows and bytes survive `Unavailable`, expired URL, `Dead`, validation and missing-file outcomes | Local photographs remained after the interruption; catch details stayed synced |
| Robust interruption, auth, retry and dependency tests | Auth expiry pauses without consuming an attempt; exact backoff values; `Dead` after the maximum; terminal errors not retried; missing remote catch; metadata before photographs; account isolation | A Draft owned by another account was never claimed |

## Automated results

- `dotnet build FishingLogBook.sln` including the Android target: 0 warnings, 0 errors.
- `dotnet test FishingLogBook.sln`: 4,335 passed (Mobile 1,223, Web 1,869, Application 597,
  Infrastructure 291, Api 289, Shared 56, Db.Migrations 10); nothing skipped.
- `npm test`: 409 passed. `npm run test:browser`: 159 passed, 7 existing skips. No Web code
  changed.
- Mobile tests rose from 926 to 1,223. They use real SQLite for every persistence and
  coordinator case; HTTP and storage are NSubstitute stand-ins backed by dictionaries so the
  tests assert resulting server and local state, not only calls.
- A defect found by the tests and fixed: coalescing a `Pending` operation cleared its
  `lasterror`, so a repeated 404 looped instead of becoming terminal.
- A defect found on the device and fixed: the first run after startup ran before the account
  lease existed and nothing re-triggered it. The account session coordinator now raises
  `SessionActivated`, which the sync coordinator subscribes to.

## Android emulator

Emulator `emulator-5554` (Android 16, API 36), Debug build of this branch, signed in against
the **shared dev API**. Local state was read with `sqlite3` through `run-as`.

| Check | Result |
| --- | --- |
| Upgrade from schema 2 to 3 with three existing outbox rows | Passed; rows preserved |
| Startup online syncs the existing saved catches automatically | Passed; both catches synced (revisions 14 and 10); four photographs went upload URL, PUT, record to `Recorded` with object keys |
| A Draft owned by another account | Passed; never claimed, remained `Pending` |
| Offline edit saved locally, nothing sent | Passed; revision 15, one `Pending` operation, 0 attempts, list shows "Waiting to sync" |
| Reconnect syncs the edit automatically, without duplicates or photograph re-uploads | Passed |
| New catch with three photographs (two JPEG, one PNG) recorded offline | Passed; three durable photograph rows, one `Pending` operation, nothing sent |
| Upload interrupted by cutting the network mid-flight | Passed; details stayed synced, list read "Details synced · 0 of 3 photos uploaded. Sync needs attention." |
| Recovery after reconnecting | Passed; all three `Recorded`, metadata operation ran once |
| Persisted status and manual retry | Passed with a **simulated** failure: one photograph operation was set to `Failed` in the local database, the detail page showed the partial status and Retry sync, tapping it completed the upload and the status became "Synced." |

Screenshots: [list synced](evidence/272/list-synced.png),
[offline waiting](evidence/272/list-offline-waiting.png),
[interrupted upload](evidence/272/list-photos-interrupted.png),
[retry available](evidence/272/detail-retry.png),
[synced after retry](evidence/272/detail-synced-after-retry.png).

### Not verified

- **Server state was not read independently.** `flyctl` was not authenticated and no API or
  Web credentials were used, so "exactly one catch and all photographs exist on the server"
  and the Web view are inferred from the server's 2xx responses recorded as local state
  (`syncedrevision`, `Recorded` with object keys), not observed. This still needs a check in
  the Web application with the test account.
- **Offline cold restart**: an offline launch asks for the device credential, which was not
  available, so kill-and-restart while offline was not run on the device. Durability across
  restarts is covered by the SQLite tests and the #271 device evidence; restart recovery of
  interrupted work is covered by the previous-process-lease tests.
- The terminal `Failed` state was simulated rather than produced by the server.
- Dark theme, French, TalkBack and the single-photo case were not exercised on the device.
- The two pre-existing test catches (four photographs) were synced to the dev API as a side
  effect of installing this build.

## Self review

- Issue/AC: all five criteria covered above; one criterion's server-side half is unverified.
- Architecture: coordinator is generic and account-scoped, handlers are small, no sync logic
  in views or view models; UI reads persisted state and calls only a retry service.
- Security/privacy: ownership comes from the trusted session lease; only the signed-in
  account's operations are claimed; presigned URLs must be https and are requested fresh per
  attempt; logs carry ids and exception types only (no tokens, bytes or coordinates).
- Database: additive migration, names follow `database.md`, state tables are not cascade
  deleted by photograph replacement.
- Findings fixed: lost `lasterror` on coalescing, missing startup trigger.
- Remaining gaps: listed in [mobile-catch-sync.md](mobile-catch-sync.md) (last-write-wins
  across devices, trips deferred to #273, foreground only, singleton clients captured from a
  transient registration as elsewhere in the app).
- Deployment: no backend, Terraform or cloud change; the SQLite migration runs on startup.
