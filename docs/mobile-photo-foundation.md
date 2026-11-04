# Mobile photo foundation

Native MAUI photograph picking, preparation, durable storage, and SQLite path references for catch photos (issue #270).

## Components

| Area | Types |
|------|--------|
| Shared EXIF | `FishingLogBook.Shared.Photographs` — `PhotographMetadataProcessor`, models, `IPhotographLocalTimeMapper` |
| Mobile services | `Features/Photographs/Services/` — preparation, collection editor, orphan cleanup |
| Mobile stores | `Storage/Stores/PhotographFileStore`, `Offline/Stores/SqlitePhotographReferenceStore` |
| Mobile providers/mappers | `Providers/PhotographRootPathProvider`, `Mappers/` (save-args + local-time) |
| IO adapters | `IPhotographMediaAdapter` (MAUI `MediaPicker` / permissions in `Native/Adapters/PhotographMediaAdapter`) |
| SQLite | `localcatchphotographs.localfilepath` stores **relative** paths under the photograph root |

## Storage layout

Root directory:

- Default: `{AppDataDirectory}/photographs`
- Android: same parent folder as the no-backup SQLite file when `ILocalDatabasePathProvider` is registered

Relative path stored in SQLite:

```text
{userId:N}/{photographId:N}.jpg
```

(`.png` / `.webp` when content type matches.)

Writes use a temp file in the account folder, then an atomic move. Failed writes remove the temp file and do not return a saved relative path.

## Compression and formats

| Format | Policy |
|--------|--------|
| JPEG, PNG, WebP | Detected from byte signatures; preparation sanitises (EXIF/GPS/comments stripped, orientation retained) without recompression and saves using the detected MIME type and extension |
| HEIC/HEIF | Unsupported bytes remain unsupported; a filename or declared MIME type alone does not establish the image format |

Maximum size: 10 MiB (`PhotographPreparationService.MaxPhotographBytes`), matching Web.

iOS camera capture explicitly requests quality 85, maximum width/height 2560,
rotation, and no metadata preservation. Gallery picking retains its existing flow.
The final bytes still pass through the shared privacy sanitiser and the 10 MiB
limit before durable storage. Oversized sources return the size-specific outcome;
reads stop after the limit plus one byte, including for non-seekable streams.

## iPhone camera preparation investigation (#247 / PR #299)

Reported evidence: TestFlight build 3 on an iPhone returned “This photo could not
be prepared” for a new camera photo; choosing a gallery photo worked. Device model,
iOS version, the camera file bytes, MIME type and size were not supplied. This is
not evidence that HEIC caused the failure.

The pinned MAUI 10.0.120 implementation has a camera `UIImage` fallback whose
default quality and missing size options select a full-resolution PNG. See
[`CompressedUIImageFileResult` in MAUI](https://github.com/dotnet/maui/blob/10.0.120/src/Essentials/src/MediaPicker/MediaPicker.ios.cs).
Our previous oversized-file read returned null before the size check, producing
the reported generic error. This establishes a reproducible code path, not the
actual format or size of build 3's failed photo. Explicit capture options avoid
that default PNG branch. Byte detection also handles supported images with
missing or inaccurate MIME declarations.

`PhotographMetadataProcessor` was inspected without changing its format parsers.
Regression tests exercise its real sanitiser with encoded PNG/JPEG bytes, all
eight JPEG orientations, private APP1 data, and the real file store. They verify
durable bytes after source deletion and store recreation. Existing gallery
timestamp proposals, shared JPEG scan tests and SQLite/outbox tests remain in the
validation suites. Preparation succeeds only after the file store finishes its
write/flush/atomic move; SQLite attachment and subsequent upload remain separate.

Diagnostics distinguish picker cache copy, bounded source read, detected format
and declared-type agreement, size rejection, sanitisation, and durable save
(directory, write/flush, atomic move). They record safe format names, source enum,
byte count, outcome or exception type only. They do not record file paths, raw
untrusted MIME strings, exception messages, image bytes, EXIF, GPS or capture time.
The iOS console provider accepts only explicitly marked pipeline events from the
three photograph categories. It excludes unrelated application logs and exception
details, and a failed diagnostic write cannot interrupt preparation. Device-console
collection on a physical iPhone remains unverified; there is no new telemetry upload.

Physical iPhone verification is still required after an explicitly authorised
future TestFlight build: take portrait/landscape photos, check upright previews,
save offline, force-kill/reopen, reconnect/sync, and repeat gallery selection.
No TestFlight build, upload, merge or deployment is part of this PR update.

Maximum photos in `PhotoCollectionEditorView`: bindable `MaximumCount` (default 10).

## Permissions

`PhotographMediaAdapter` requests `Permissions.Camera` for capture and `Permissions.Photos` for gallery multi-select (where supported). Denied, cancelled, and unavailable outcomes map to result models without throwing.

## Orphan cleanup

`PhotographOrphanCleanupService` runs after SQLite migration via `ILocalDatabaseInitializedObserver`.
`SqliteDatabase` finishes migration (and marks initialization complete) **before** notifying
observers, so observer work that calls `EnsureInitializedAsync` cannot deadlock.

For each account folder `{root}/{userId:N}`:

- Abandoned write temps (`.{id}.tmp`) are deleted.
- Durable files referenced by `localcatchphotographs.localfilepath` are retained (including unsynced rows).
- Durable files **without** a SQLite reference are **retained** — they may be prepared photos
  interrupted before a Catch/draft save. Reclaim of long-abandoned durable files is out of scope here.
- Other users' folders are not touched.

## Shared EXIF reuse

Web `PhotographMetadataService` delegates to Shared `PhotographMetadataProcessor` with `WebPhotographLocalTimeMapper` (`ITimeService`). Mobile uses `DevicePhotographLocalTimeMapper` (`TimeProvider` / local time zone).
