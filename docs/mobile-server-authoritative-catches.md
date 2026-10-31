# Server-authoritative Catch List and safe local cleanup (#285)

The server is the source of truth for confirmed catches. SQLite keeps only drafts,
unsynchronised changes, failed operations, the photographs they need, and a transient
working copy while a server-backed catch is being edited. Outbound synchronisation (#272)
is unchanged.

## Investigation findings (no scope change needed)

- **No backend change.** `GET /api/catches` returns every catch the user caught or recorded as
  `CatchViewDto` (unpaginated; no pagination is invented here, scaling is future work).
  `GET /api/catches/{id}` returns one, `POST /api/catches` upserts.
- **Photographs.** The view DTO carries a presigned download `Url` per photograph (valid for
  one hour) and the server only issues it when the object exists in storage, so a non-null
  `Url` is also proof that the bytes are stored.
- **Server-backed Edit is feasible.** The server's upsert keeps the stored location columns
  when an incoming upsert omits them, and for the catching user it returns the exact location,
  so a working copy loses nothing. Photographs are downloaded into the existing durable store
  under their original ids, which preserves identity (no re-upload; removal still uses the
  existing DELETE-before-upsert).
- **Limits of editing.** A catch can only be edited here when the signed-in user recorded it
  (trip and participant provenance is #273), and hydration needs a connection. Both cases show
  a clear message instead of a broken editor.
- **The existing orphan cleanup does not delete unreferenced durable files** (it retains them
  on purpose), so it cannot recover files left after a crash. A persisted deletion queue is
  therefore required (migration 004).

## Online and offline behaviour

`CatchHistoryService` builds the list: the server list overlaid with local records, deduplicated
by catch GUID.

- A local draft always shows (as "Draft").
- A local saved catch that is not fully synchronised shows the **local** version.
- A local saved catch that is fully synchronised and present on the server shows the **server**
  version. A synchronised local catch the server does not return stays local (never lost).
- Offline or API failure: only local records are shown, with an explicit notice (offline,
  server unreachable, sign-in required). An API failure is never shown as an empty history; with
  no local records the page shows an Offline or Error state, not "no catches".
- Server catches open in the normal detail page with their remote photograph URLs. Nothing from
  the server list is persisted.

## Server-backed Edit

`CatchWorkingCopyService.EnsureAsync` runs when the editor opens a catch that has no local record
(a local record always wins). It reads the catch, downloads every photograph through the file
store, and creates in one transaction a Saved row at revision 1 with the synchronised state
already recorded and **no outbox operation**. The editor then behaves exactly as for any local
catch; saving bumps the revision and the normal outbound sync sends it. Failures roll back the
downloaded files, but only when no row references them, because file names are derived from the
photograph id.

## Cleanup

`LocalCatchCleanupService` runs after every sync run, at account activation, and independently of
the list. A local catch is removed only when all of these hold:

1. Saved, synced revision at least the current revision, every photograph `Recorded`, and no
   outbox row that is not `Completed` for the catch or its photographs.
2. It is not open in an editor (an in-memory guard checked before and after the network call).
3. The server copy is re-read and has exactly the same photograph ids, each with a download URL.
4. A single transaction re-checks (1), including that the revision is unchanged, deletes the
   catch, photographs, sync state and completed outbox history, and queues the photograph file
   paths in `localpendingfiledeletions`.

Files are deleted after the commit through `IPhotographFileStore.TryDelete`, which only resolves
paths inside the account's own photograph folder (so it cannot touch the gallery), and a queued
path that a row references again is dropped, not deleted. A crash between the commit and the file
delete is recovered from the queue on the next run, with no network needed.

## Limitations

- Concurrent edits from two devices remain last-write-wins (no server concurrency token).
- The list is unpaginated and presigned URLs last an hour, so a list left open that long needs a
  refresh for images.
- Converting the exact consent version assumes the current single version.
- A catch that fails server verification is re-verified on each later run.
