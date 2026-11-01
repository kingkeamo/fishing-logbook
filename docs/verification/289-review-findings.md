# PR 289 review fixes

## Findings

1. Confirmed: `Trip.ReconciledWith` protected actual start only for Active→Active. A stale offline completion could replace the start established by an online participant. Reconciliation now retains the actual start when completing an activated plan, and retains its completed lifecycle on subsequent replays. Ordinary, unplanned trips retain their existing timestamp-editing behaviour. The repository validates the reconciled lifecycle inside its row-lock transaction.
2. Partly already fixed: Mobile `TripStartService` already had a localized retry/cancel loop for typed lookup failures at the reviewed head. Mobile transport exceptions now produce the same failed lookup. Web swallowed server and local-store failures as empty lists; these now propagate to the existing localized error UI, preventing automatic creation of an unrelated trip. Offline lookup still uses local plans without a server request.
3. Confirmed: the native Catches action refreshed after activation failure without showing feedback. Its ViewModel now owns error state, displayed by `TripActionView` with the existing error colour, semantics and automation ID. Both native entry points handle unexpected activation failures while propagating requested cancellation.
4. Confirmed: the merged eligibility list resurrected previously synchronized local plans absent from a successful authoritative server result. Online availability now comes from the server for those plans, including plans with queued offline edits. Truly new unsynchronized local plans remain available. Offline cached plans remain available, and pending local work is never deleted by eligibility lookup. Pending local activation/cancellation suppresses the corresponding server plan until synchronization.
5. Confirmed: planned trips could be reactivated by stale Active upserts after completion. Planned-trip completion is now monotonic. PostgreSQL coverage exercises accepted membership, participant activation, stale organiser completion, replay, one trip, both users' history and retained contributions. Client synchronizer tests exercise authoritative lifecycle read-back through real SQLite on Mobile and the existing stateful Web store harness. Web now handles lifecycle conflicts by re-reading the server and rebasing the queued metadata; Mobile no longer discards queued title/place changes on that path. A second write failure does not confirm local work.
6. Metadata uses the existing last-accepted-write policy, not device-clock ordering. Under the repository row lock, the last accepted owner upsert supplies title, place name and location, including clearing nullable values. While still Planned, planned start is editable under that same policy. After activation, the stored planned start and actual start take precedence; completed planned trips cannot return to Active. No version token or unrelated conflict framework was introduced. Older offline metadata can therefore replace newer metadata on synchronization; this is an explicit limitation of the existing API contract. PostgreSQL regression coverage locks down that policy.

## Boundaries

- Existing APIs, authorization and invitation permissions are reused. No authentication, infrastructure or migration changes were added by this review fix.
- A failed authoritative read never triggers cleanup or removal of pending work.
- A conflict reread that still reports Planned is not treated as successful synchronization. Local work remains pending for resolution rather than being silently downgraded.
- PostgreSQL tests prove the authoritative persistence boundary; client tests prove their read-back/persistence paths. They are not a claim that two physical devices were exercised together.

## Verification

Final test, build, CI and emulator results are recorded below after validation completes.

## Self review

- Issue/AC: reviewed each supplied finding against the current branch; existing Mobile retry behaviour retained.
- Architecture/CQRS: existing domain reconciliation, repository transaction, clients, synchronizers and native ViewModels retained.
- Rules: localized EN/FR text and matching test conventions; no new presentation abstraction.
- Security/privacy: owner checks remain at both server and reconciliation boundaries; no credentials or precise coordinates in evidence.
- Database/migrations: row-lock reconciliation tested with PostgreSQL; no new migration.
- Tests/coverage: regression tests include actual-start preservation, terminal completion, ordinary-trip edits, invalid reconciled end, stale eligibility, failed lookup, localized feedback and client reconciliation.
- Browser: existing browser suite run separately; bUnit covers Blazor lookup-error interactions. Browser tests do not validate native XAML.
- UI/localization: native error uses shared styling, semantics and automation ID; resource keys remain paired.
- Code smells: removed obsolete fallback that concealed lookup failure; kept existing service boundaries.
- CI/deployment: existing planned-trip migration still needs deployment before exercising the new server API on a deployed environment.

Review fixes included retaining queued metadata during lifecycle-conflict recovery and rejecting an end before the server's authoritative start. Device verification and final suite results must be assessed independently of unit-test counts.
