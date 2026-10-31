# Native photograph components

The native photo editor follows the Web composition of carousel, metadata details,
and picker. It uses native MAUI views and the existing photo foundation; it does
not host Razor or a WebView.

| View | Inputs | Events |
| --- | --- | --- |
| `PhotoCarouselView` | Photo path, position text, photo hint, multiple-photo flag, editable/removal flags, localisation | Photo tapped, previous, next, remove |
| `PhotoDetailsView` | Formatted date, location availability, apply availability, chosen state, optional apply action, localisation | Use details |
| `PhotoPickerView` | Capacity, busy state, status, localisation | Camera, gallery |

These views can be used independently. They have no Catch dependency, persistence,
permissions, or API calls. Read-only consumers leave `IsEditable` and
`ShowUseDetails` false. The parent supplies state and handles events.

The Catch detail page is the read-only consumer: it binds the carousel to
`CatchPresentationModel` (selected photo, position text, multiple-photo flag) and
handles `PhotoTapped` by opening the editor. `PhotoTapped` is raised only by the
main image; previous, next and the position label are separate controls outside
it and never raise it.

`PhotoCollectionEditorView` composes the three views inside an expandable section.
Its `ViewModel` binding retains the caller's binding context; only its contents
bind to `PhotographCollectionEditorViewModel`. This avoids invalidating the
caller's `ViewModel="{Binding Photographs}"` binding.

The existing photo ViewModel owns the collection, active photo, applied-details
indicator, navigation, picker actions, and minimum/maximum count guards. Browsing
wraps in either direction and never reorders or persists photos. New selections
show the last added photo. Removal chooses a remaining photo and protects the
minimum count. The ordering service and ViewModel actions remain available;
carousel arrows navigate, matching Web, rather than changing stored order.

`UseDetailsRequested` lets the owning feature apply metadata. Catch handles this
in `CatchEditViewModel`, using the shared proposal service and private location
defaults. Merely browsing photos does not change the catch's date/location.
Existing saved catches still persist only on explicit Save, including photo
additions, removals, and metadata choices. Web's immediate photo persistence and
sync orchestration are intentionally not copied.

Camera/gallery continue through `IPhotographMediaAdapter`, preparation through
`IPhotographPreparationService`, and durable bytes through
`IPhotographFileStore`. SQLite retains relative references and photo metadata via
the additive version 2 migration. A partially invalid multi-selection retains
valid prepared photos and reports the failure. Removal never deletes durable
bytes; reference-aware cleanup remains responsible for file lifecycle.

All labels, counters, metadata fallbacks, and chosen-state copy have English and
French resources. The controls use the existing theme dictionaries, 48dp actions,
accessible descriptions, and stable automation IDs.
