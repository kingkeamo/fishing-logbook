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
| JPEG, PNG, WebP | Allowed; **no recompression** — bytes are sanitised (EXIF/GPS/comments stripped, orientation retained) and copied |
| HEIC/HEIF | **Not** in `PhotographContentTypeConstants.Allowed`; preparation returns unsupported unless the platform adapter supplies an allowed type |

Maximum size: 10 MiB (`PhotographPreparationService.MaxPhotographBytes`), matching Web.

Maximum photos in `PhotoCollectionEditorView`: bindable `MaximumCount` (default 10).

## Permissions

`PhotographMediaAdapter` requests `Permissions.Camera` for capture and `Permissions.Photos` for gallery multi-select (where supported). Denied, cancelled, and unavailable outcomes map to result models without throwing.

## Orphan cleanup

`PhotographOrphanCleanupService` runs after SQLite migration via `ILocalDatabaseInitializedObserver`. For each account folder `{root}/{userId:N}`, it deletes files not referenced by any `localcatchphotographs.localfilepath` row for that user (including unsynced rows). Referenced files are never deleted; other users' folders are not touched.

## Shared EXIF reuse

Web `PhotographMetadataService` delegates to Shared `PhotographMetadataProcessor` with `WebPhotographLocalTimeMapper` (`ITimeService`). Mobile uses `DevicePhotographLocalTimeMapper` (`TimeProvider` / local time zone).
