# Mobile SQLite persistence (PR 1 / #269)

Local-first Catch metadata and a generic transactional outbox for the native MAUI
app. No UI, photo capture, or remote sync in this foundation.

## Provider

**Microsoft.Data.Sqlite** with raw parameterised SQL.

Chosen to match the existing Dapper / SQL-first style on the server, keep the
dependency small on `net10.0` (including the unit-test host TFM), and avoid EF
Core on device. Migrations are versioned embedded `.sql` scripts applied by
`SqliteMigrator`.

Identifier naming follows the same project rules as PostgreSQL: lowercase, no
underscores (see `.claude/rules/database.md`). Mirrored catch columns use the
same names and units as the server (`weight` kilograms, `length` centimetres,
`createdon`, …). Local-only columns/tables are allowed when needed.

## Schema overview

| Table | Role |
|-------|------|
| `schemainfo` | Single-row applied migration version |
| `localtrips` | Minimal trip stubs referenced by catches |
| `localcatches` | Account-scoped catch metadata + revision + lifecycle |
| `localcatchphotographs` | Photo metadata rows (`ON DELETE CASCADE` from catch) |
| `outboxoperations` | Generic pending sync intents (Catch, Trip, …) |

GUIDs are `TEXT`; timestamps are ISO-8601 `TEXT`.

Pending outbox rows coalesce via a partial unique index on
`(userid, aggregatetype, aggregateid, operationtype) WHERE status = 'Pending'`.

### Outbox lifecycle invariant (concurrent edit while syncing)

Statuses: `Pending` → `InProgress` (claim) → `Completed` / `Failed` / `Dead`.

`UpsertPendingAsync` only coalesces rows that are still `Pending`. When sync claims a
row by moving it to `InProgress` (or any non-`Pending` status), a later local edit
creates a **new** `Pending` row. Completing or failing the claimed row updates that row
only and must not clear or rewrite the newer `Pending` intent.

The unique index enforces at most one `Pending` row per
`(userid, aggregatetype, aggregateid, operationtype)`. Multiple historical rows may
exist for the same aggregate once statuses leave `Pending`.

Future sync must treat each outbox row as revision-scoped: completing revision *N*
must not mark the local catch synchronised if `localcatches.revision` is already
greater than *N*, and must not overwrite newer local fields from an older payload.

### Outbox payload vs local catch (location)

The pending `UpsertCatchMetadata` payload is a **revision-scoped snapshot** of catch
metadata, including location coordinates, accuracy, captured-on, source, visibility
and consent version when present (aligned with Web `CatchDto` / `CatchLocationDto`).
Sync may POST from that snapshot for the claimed operation. The durable local catch
row remains the live device state; privacy fields stay on-device until an authenticated
sync uses them. This PR does not implement sync or coordinate disclosure policy.

## Atomic save + outbox

`ICatchStore.SaveAsync` (in `Features/Catch/Offline/Stores`, mirroring Web) opens one
SQLite transaction that:

1. Upserts an optional `localtrips` stub when `tripid` is set
2. Upserts the catch and replaces photograph rows
3. Upserts a pending `UpsertCatchMetadata` outbox operation (coalesce on conflict)

Commit succeeds only when catch state and outbox intent are both durable. Mid-
transaction failure rolls both back. The store never calls the API.

Shared SQLite/outbox infrastructure lives under `Common/Offline/` (like Web
`Common/Offline`).

## Account isolation

Every read and write is scoped by the signed-in `userid` from
`IAccountSessionCoordinator`. Account A cannot read or mutate account B’s rows.
Logout must **not** delete local catch or pending outbox data in this PR;
pending work remains until a later sync/cleanup design.

## Backup exclusion

- **Android:** database path uses `Context.NoBackupFilesDir` via
  `ILocalDatabasePathProvider` so Auto Backup does not include the file.
  `android:allowBackup="false"` already disables full-app backup.
- **iOS:** database lives under the app sandbox `FileSystem.AppDataDirectory`.
  Marking the file with `NSURLIsExcludedFromBackupKey` is a follow-up TODO if
  iCloud/device backup of Documents/Library paths becomes a concern.

## Encryption options

| Option | Pros | Cons |
|--------|------|------|
| **Sandbox-only, unencrypted file (this PR)** | Simple; no key management; OS process isolation | Device compromise / forensic access can read the file if the sandbox is broken |
| **SQLCipher / SEE** | Encryption at rest | Key storage, unlock UX, migration, and key-loss recovery; larger dependency |

**Recommendation for PR 1:** unencrypted SQLite in app-private storage, excluded
from cloud backup. Catch notes/location are sensitive but tokens remain in
platform secure storage, not SQLite.

**Key-loss risk (if SQLCipher is added later):** losing the encryption key makes
local catches and outbox unreadable. Prefer deriving or wrapping the DB key from
hardware-backed secure storage with an explicit recovery story before enabling
encryption. Do not encrypt until that path is designed.

**Follow-up:** re-evaluate SQLCipher (or platform file encryption) before shipping
broad offline production use if threat modelling requires encryption at rest beyond
sandbox isolation.
