# Mobile Catch and photograph synchronisation (#272)

Independent, foreground-driven synchronisation of locally saved catches and
photographs to the existing API. Local Save never calls the network; the Catch UI
reads persisted state and never calls the API or the coordinator's retry logic.

## Existing contracts reused (no new endpoints)

| Step | Endpoint | Behaviour relied on |
| --- | --- | --- |
| Create/update catch | `POST /api/catches` | Upsert keyed by the client GUID; owner check; **requires at least one photograph in the DTO**; replaces the server photograph rows when the id set changes |
| Upload URL | `POST /api/catches/{id}/photographs/upload-url` | Requires the catch **and** the photograph row to already exist and the content type to match; presigned PUT valid 15 minutes |
| Bytes | `PUT <presigned url>` | Anonymous request to object storage; same key is overwritten on repeat |
| Confirm | `POST /api/catches/{id}/photographs` | Validates the object key only; persists nothing |
| Remove | `DELETE /api/catches/{id}/photographs/{photoId}` | Deletes the object, then the row; 404 when already gone |

## Backend gaps and decisions

1. **No concurrency token.** The server is last-write-wins. Revision safety is therefore
   client-side: operations for one catch run serially on one device, and a response never
   writes back into local rows. Two devices editing the same catch remain last-write-wins;
   that is a limitation, not changed here.
2. **Idempotency.** `POST /api/catches` is keyed by the client GUID, so a retry after a lost
   response updates the same row. Repository tests (`WhenTestingUpsert`) prove repeated
   upserts keep one row. No backend change is required.
3. **"Record" is validation only.** The authoritative completion predicate for a photograph
   is therefore *PUT succeeded and Record returned 204*, stored per photograph.
4. **Server photograph-row replacement does not delete stored objects.** A photograph that
   was uploaded and later removed locally is deleted through the existing DELETE endpoint
   *before* the next upsert, otherwise the object could never be deleted afterwards.
5. **Missing remote catch/photograph (404 on upload-url or record).** Treated as a lost
   server record: the catch is re-upserted once, then the photograph retried; a second 404
   is terminal.

No backend change is required for #272.

## Persisted state (migration 003, additive)

- `outboxoperations` gains `leaseowner` and `leaseexpiresat`. Statuses: `Pending` (also
  scheduled retry: `availableat` in the future), `InProgress` (claimed under a lease),
  `Completed`, `Failed` (terminal, needs the user), `Dead` (retries exhausted, manual retry
  allowed).
- `localcatchsyncs`: highest local revision the server has accepted, per catch.
- `localphotographsyncs`: per photograph `Pending` / `Uploaded` (PUT done) / `Recorded`, with
  the object key. Kept apart from `localcatchphotographs`, which is rewritten on every save.
- New operation types: `UploadCatchPhotograph`, aggregate type `CatchPhotograph` (aggregate
  id = photograph id, so coalescing is per photograph).

## Flow

```text
Triggers --> OutboxSyncCoordinator (one run at a time, per signed-in account)
               recover expired leases -> claim next due operation -> synchroniser -> persist
Catch metadata synchroniser:  delete removed-but-uploaded photographs -> POST /api/catches
                              -> (one transaction) complete op, record synced revision,
                                 enqueue UploadCatchPhotograph for unrecorded photographs
Photograph synchroniser:      upload-url -> PUT bytes -> Record -> (one transaction) complete op,
                              mark photograph Recorded
```

Photograph operations only exist after the metadata operation succeeded, which gives the
server-required ordering without dependency tracking. A catch whose metadata synced stays
synced if a photograph fails; the photograph state is reported separately.

## Revision safety

Each save increments `localcatches.revision` and enqueues a new `Pending` operation when the
previous one is already claimed. The synchroniser always sends the live local row and records
the revision it read. Completing records `max(syncedrevision, sentrevision)` and never edits
the catch. If the user saved again while the request was in flight, the newer `Pending`
operation is still there and is sent next. Operations whose catch is already synced at the
current revision complete without a request.

## Retry, leases and recovery

- Backoff: `min(15 min, 15 s * 2^(attempt-1))` with +/-20 % jitter from an injectable source;
  eight attempts, then `Dead`.
- Retryable: network failure, timeout, 5xx, expired presigned URL. Terminal: 400/403 from the
  API, missing local file. 401/refresh failure **pauses** without counting an attempt.
- Claim sets `leaseexpiresat`. Startup and every run first return expired leases, and leases
  owned by a previous process, to `Pending`. A crash mid-request therefore retries.
- Draft catches are never sent; their operation completes as a no-op.

## Triggers (no polling)

Session lifecycle reaching `Online` (connectivity returning, token usable again), the
account session becoming active (`IAccountSessionCoordinator.SessionActivated`, which is
what starts the first run after startup because the lifecycle can reach `Online` before the
account lease exists), app returning to the foreground, work being enqueued by a local save
while online, an explicit retry, and a single timer for the next scheduled retry.
Backgrounding or leaving `Online` pauses the run. Device-offline failures release the
operation without counting an attempt.

## Ordering and coalescing details

- Claim order runs every non-upload operation before `UploadCatchPhotograph`, then oldest
  first. Together with "photograph operations only exist after the metadata succeeded" this
  gives the server-required ordering, and the metadata re-upsert that follows a missing
  remote catch runs before the photograph is retried.
- Coalescing an already-`Pending` operation updates its payload and `availableat` but keeps
  its `attemptcount` and `lasterror`. Losing `lasterror` made a repeated 404 loop instead of
  becoming terminal; this is covered by a store-level regression test.
- Operations of an account other than the signed-in one are never claimed (observed on the
  emulator: a Draft owned by another account stayed `Pending`).

## Known limitations

- The server has no concurrency token; two devices editing one catch is last-write-wins.
- `TripId` is sent as stored. Trips are not synchronised here (#273), so a catch pointing at
  a trip that does not exist on the server is rejected as invalid and becomes `Failed`.
- Removing a photograph that was uploaded deletes its remote object through the existing
  DELETE endpoint before the next upsert; if that DELETE keeps failing the catch update is
  held back (retried with backoff, then `Dead`, then manual retry).
- A failure while persisting an outcome (for example a SQLite error) leaves the operation
  `InProgress` until its lease expires (5 minutes) or the next run reclaims it.
- Synchronisation is foreground only. It does not run while the app is backgrounded.

## Status presentation

Read from SQLite only: *Waiting to sync*, *Synchronising*, *Synced*, *Needs attention*
(retrying or retries exhausted; Retry offered), *Action required* (terminal; Retry offered).
Photographs are summarised separately (for example "2 of 3 photos uploaded").
