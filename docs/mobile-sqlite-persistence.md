# Mobile SQLite persistence (PR 1 / #269)

Local-first Catch metadata and a generic transactional outbox for the native MAUI
app. No UI, photo capture, or remote sync in this foundation.

## Provider

**Microsoft.Data.Sqlite** with raw parameterised SQL.

Chosen to match the existing Dapper / SQL-first style on the server, keep the
dependency small on `net10.0` (including the unit-test host TFM), and avoid EF
Core on device. Migrations are versioned embedded `.sql` scripts applied by
`SqliteMigrator`.

## Schema overview

| Table | Role |
|-------|------|
| `schema_info` | Single-row applied migration version |
| `local_trips` | Minimal trip stubs referenced by catches |
| `local_catches` | Account-scoped catch metadata + revision + lifecycle |
| `local_catch_photographs` | Photo metadata rows (`ON DELETE CASCADE` from catch) |
| `outbox_operations` | Generic pending sync intents (Catch, Trip, …) |

Canonical measurements are stored as `weight_grams` and `length_millimetres`
(`REAL NULL`). GUIDs are `TEXT`; timestamps are ISO-8601 `TEXT`.

Pending outbox rows coalesce via a partial unique index on
`(user_id, aggregate_type, aggregate_id, operation_type) WHERE status = 'Pending'`.

## Atomic save + outbox

`ILocalCatchService.SaveAsync` opens one SQLite transaction that:

1. Upserts an optional `local_trips` stub when `trip_id` is set
2. Upserts the catch and replaces photograph rows
3. Upserts a pending `UpsertCatchMetadata` outbox operation (coalesce on conflict)

Commit succeeds only when catch state and outbox intent are both durable. Mid-
transaction failure rolls both back. The service never calls the API.

## Account isolation

Every read and write is scoped by the signed-in `user_id` from
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
