# Historical Photo Import Investigation

**Issue:** #212, design gate for #150  
**Related slices:** #213–#222  
**Status:** Recommendation only; no production Import implementation is included

## Decision summary

The first Import milestone should be a transient, mobile-first wizard. It should reuse the current client-side metadata parser and the existing local-first Trip/Catch/photo persistence and synchronisation paths. It should not introduce an Import database model or a parallel upload API.

Photos should be processed sequentially, grouped deterministically into proposed Catches using trustworthy timestamps, and reviewed by the user. The reviewed Catches should then be evaluated separately for likely historical Trip groupings. The user may create a Trip, choose a compatible existing Trip, or keep catches separate; the application must never create or assign a Trip without confirmation. Confirmed state becomes ordinary `TripModel` and `CatchModel` records. Reverse geocoding is optional review enrichment supplied through a provider-agnostic, server-side lookup; accepted EXIF coordinates remain canonical.

The first usable flow is:

```text
choose method/species
  -> select photos and extract metadata
  -> propose same-catch photo groups
  -> review/correct Catches
  -> propose likely Trip groups from reviewed Catches
  -> user chooses create Trip / existing Trip / no Trip
  -> persist confirmed Trips, Catches and relationships
```

## 1. Current photo pipeline

The existing path is:

```text
RecordCatchEditor
  -> PhotographPicker (InputFile)
  -> PhotographPreparationService
  -> PhotographMetadataService + PhotographSanitisationService
  -> PreparedPhotographModel / CatchPhotographModel
  -> IndexedDbCatchStore
  -> CatchSynchroniser
  -> Catch metadata API
  -> presigned object-storage PUT
  -> photograph-record API
  -> authoritative Catch reread/reconciliation
```

- `PhotographPicker` offers a camera input and a gallery input. Gallery selection already uses `multiple`; the component limits a selection to 10 files and prepares them sequentially.
- `IBrowserFile` is opened and copied into a managed `byte[]`. The current maximum is 10 MiB per photograph, and JPEG, PNG and WebP are supported.
- Gallery files are parsed for metadata. The camera path intentionally supplies empty metadata because it represents a new catch rather than a historical-photo workflow.
- Sanitisation removes EXIF/XMP and other metadata while retaining the orientation information needed to display the image correctly.
- `PreparedPhotographModel`, local Catch storage and synchronisation currently retain full photograph bytes. `PhotographCarousel` additionally builds base64 data URLs, adding memory overhead.
- Confirmed catches are saved locally first. Synchronisation upserts Catch metadata, obtains a presigned upload URL for each photograph, uploads its bytes, records the photograph, and rereads authoritative server state. Partial photo failures remain retryable.
- Existing coverage includes metadata parsing/sanitisation, preparation, picker selection, Record Catch metadata/location behavior, proposal behavior, IndexedDB/browser helpers, synchronisation, API/application/infrastructure persistence and online Catch E2E tests.

The Import workflow can reuse the parsing and sanitisation rules, catalogue pickers, Catch location semantics, Catch models, local store and synchroniser. It should not reuse the current full-byte/base64 preview lifetime for a larger batch, and it must not alter the behavior of Record Catch.

## 2. Reusable code and dependencies

`PhotographMetadataService` is repository-owned parsing code; there is no third-party EXIF or geocoding dependency to reuse. It currently understands:

- JPEG, PNG and WebP containers;
- EXIF `DateTimeOriginal` and `DateTimeDigitized`, preferring original;
- `OffsetTimeOriginal` and `OffsetTimeDigitized` when present;
- GPS latitude/longitude and their hemisphere references;
- orientation;
- plausible file `LastModified` as a weak fallback.

Metadata extraction is client-side and does not require current-device geolocation permission. A selected browser `File` is the source of the original bytes visible to the application. The repository cannot guarantee that a mobile picker or operating system has not stripped metadata or transcoded a photo before supplying that `File`; that requires physical-device validation.

The existing parser is sufficient for the first slice. #214 should extend or wrap it only where the Import transient representation needs richer timestamp state or browser-managed blobs. No new EXIF package is justified by this investigation.

## 3. Platform limitations

Code and automated tests prove behavior after a browser has supplied a file. They do not prove the behavior of each device's native picker.

- Android installed PWA: multi-select availability, original-byte retention, HEIC conversion and metadata retention are device/browser/provider dependent and unproven here.
- iPhone Home Screen PWA: picker multi-select, Photos privacy choices, HEIC/conversion behavior, memory pressure and metadata retention are unproven here.
- Desktop: current browser APIs and test coverage provide the best confidence, but source-application exports may still strip EXIF.

The product must treat missing metadata as normal, never as corruption or user error.

## 4. Multi-select and memory recommendation

Start with a configurable maximum of 20 photographs per Import batch, retaining the existing 10 MiB per-file validation. Twenty is enough to make grouping useful without assuming that Wasm can safely hold hundreds of decoded images. Real-device results may justify lowering or raising it.

Do not retain every full image as a managed `byte[]` plus a base64 preview. #214 should use an Import-specific browser-side blob registry:

1. Accept the selected files and process them sequentially.
2. Bring one file into managed memory for validation, metadata extraction and sanitisation.
3. Store the sanitised result as a browser-managed `Blob` behind an opaque transient token.
4. Generate a small review thumbnail/object URL; never decode all full-resolution images together.
5. Release the managed buffer before processing the next file.
6. Retrieve one blob at a time when a confirmed Catch is handed to existing persistence.
7. Revoke object URLs and delete blob-registry entries on photo removal, batch cancellation, navigation/disposal and successful handoff.

Selection, preparation and persistence need cancellation tokens. A new selection or explicit cancel should stop remaining work and clean up completed transient resources. Sequential processing is the default; bounded parallelism is unnecessary for V1.

## 5. Transient import model

Names are illustrative rather than prescribed contracts.

### Import batch

- transient batch identity and stage;
- selected Fishing Method and Species, including stable catalogue identity and display values;
- ordered selected photos;
- ordered proposed Catches;
- ordered Trip proposals and the user's decision for each independent cluster;
- preparation/lookup/confirmation progress and cancellation state.

### Selected photo

- transient identity and stable selection index;
- optional filename for display only, never identity;
- content type and byte size;
- opaque blob token and thumbnail/object URL;
- metadata availability/error state;
- captured wall-clock value, optional offset/instant, source, trust and timezone-ambiguity state;
- optional EXIF coordinates;
- optional transient location-label result/status;
- duplicate signal/status;
- removed/assigned state.

### Proposed Catch

- transient identity and ordered photo identities;
- proposed `CaughtOn` plus whether user confirmation is required;
- optional proposed `CatchLocationModel` and accepted/removed state;
- inherited method/species with per-proposal overrides;
- validation and review state.

### Proposed Trip

- transient identity and ordered reviewed Catch identities;
- confidence/reason codes based on canonical dates, times and accepted coordinates;
- proposed `StartedOn`/`EndedOn`;
- optional representative accepted location and transient display context;
- decision: undecided, create Trip, selected existing Trip, or no Trip;
- optional selected existing Trip identity;
- editable membership so individual Catches can be removed.

The draft remains page/wizard state in V1. IndexedDB is not needed for the draft: it would add schema, blob lifecycle and resume semantics before they are proven necessary. A refresh before confirmation loses the batch and the UI should say so. Once confirmed, proposals become ordinary local Catch records and gain the existing durable/offline retry behavior.

## 6. Grouping recommendations

Photo grouping and Trip grouping are different operations and must remain separate:

- **Photo grouping** operates on selected photographs and decides which photographs probably show the same Catch. Its output is proposed Catch groups.
- **Trip grouping** operates later and only on reviewed Catch proposals. It decides which reviewed Catches may belong to one fishing session. Raw photo groups are never a substitute for reviewed Catch state.

### 6.1 Same-catch photo grouping

Use a two-minute inclusive threshold initially:

1. Sort photos with trustworthy timestamps by captured instant when an offset exists, otherwise by confirmed local wall time. Use selection index as the stable tie-breaker.
2. Start the first group and compare each next photo with the immediately preceding photo in that group.
3. A gap of `<= 2 minutes` stays in the group; a larger gap starts another group.
4. Materially different GPS positions may lower confidence or start a review warning, but GPS is not required and must not invent chronology.
5. Each photo with no trustworthy/confirmed timestamp starts as an explicit ungrouped singleton requiring review.

Two minutes accommodates a short sequence of catch, mat and release photographs while reducing accidental merging of adjacent catches. The existing 30-minute `CatchPhotographProposalService` window detects conflicts for one manually recorded Catch; it is too broad to define historical catches.

Keep the threshold as one named option/constant owned by the grouping service. Unit tests must cover just below, exactly at and just above the boundary, stable ties, chains, absent timestamps and mixed GPS availability. All proposals remain editable.

### 6.2 Trip suggestions from reviewed Catches

Use a deterministic two-stage V1 strategy. Partition reviewed Catches by confirmed local calendar date, sort each partition by confirmed `CaughtOn` and stable proposal identity, then build spatial/time clusters from adjacent Catches.

Recommended starting thresholds are:

- **nearby:** accepted coordinates are within 5 km;
- **clearly distant:** accepted coordinates are more than 25 km apart;
- **continuous:** the gap between adjacent reviewed Catches is no more than 4 hours;
- **maximum suggested span:** earliest to latest Catch is no more than 18 hours and remains on one confirmed local calendar date.

These are named, configurable policy values rather than hidden constants. Five kilometres allows movement around a fishery, river reach or bay while remaining conservative enough to avoid joining different venues. The 25 km veto prevents same-day travel between distinct areas being treated as one Trip. A four-hour adjacent gap accommodates quiet periods during a session; the same-date/18-hour limits prevent chains from spanning implausibly long periods. #220 should validate these values against representative historical data before release.

Suggestion confidence is:

- **strong:** two or more Catches share a confirmed local date, all available accepted coordinates are mutually compatible with the 5 km cluster, and adjacent times are continuous;
- **weaker:** the same date and continuous times are confirmed but one or more Catches lack GPS; show a proposal with that limitation rather than requiring GPS;
- **none:** coordinates prove Catches are more than 25 km apart, the time continuity/span rule fails, only one Catch remains, or any date/time needed for the cluster is still missing or ambiguous.

Distances between 5 km and 25 km are not automatically joined in V1. They can form separate proposals and the user may combine them manually if appropriate. Method consistency and location context may be displayed as supporting evidence, but a method change does not by itself split a Trip. Reverse-geocoded strings never participate in clustering.

All timezone-ambiguous timestamps must be user-confirmed before Trip suggestions run. Removing or editing a Catch reruns suggestions deterministically. The user can accept a cluster, remove individual Catches, choose a different existing Trip, or decline it entirely.

For a new historical Trip, reuse the existing model:

- `StartedOn` is the earliest reviewed Catch time and `EndedOn` is the latest;
- `OwnerUserId` is the current authenticated product user;
- status is the existing completed historical-Trip state, subject to #220 confirming the existing status rule;
- a representative accepted Catch coordinate may populate the existing private `TripLocationModel` only with explicit user confirmation;
- `PlaceName` may use the transient review label only if the user explicitly accepts/edits it and existing Trip semantics allow it; it must not be silently derived;
- leave `Title` null unless the existing Trip UI requires and obtains a user-supplied title.

Do not invent Import-only Trip fields. When online/local Trip listings are available, suggest an existing Trip only when the current user owns or can validly contribute to it, its time range overlaps or contains the reviewed Catch range, and available accepted coordinates are compatible. Never silently attach; selecting an existing Trip is an explicit decision. Offline operation may offer only locally available Trips and must not claim that the list is exhaustive.

## 7. Missing-date and timezone rules

- EXIF date/time with an explicit offset is a trustworthy instant.
- EXIF date/time without an offset is a trustworthy wall-clock reading but not a trustworthy instant. Do not silently apply the device's current timezone for historical travel. Require the user to confirm the displayed local date/time before persistence; V1 need not reconstruct the historical timezone.
- File `LastModified` is a weak suggestion only. It cannot drive automatic grouping or confirmation.
- Missing, malformed or implausible values require explicit user entry.
- A date without a time requires the user to enter/confirm a time; do not choose noon or import time.

Use a mobile `datetime-local` editor on each affected proposal, with a concise explanation that the photo did not contain a reliable timezone/date. Confirmation is blocked only for that proposal until a valid value is supplied.

## 8. GPS and privacy rules

Accepted EXIF GPS maps to the existing `CatchLocationModel`: latitude, longitude, photo-metadata source, private visibility, applicable consent version, and captured-on value where supported. No current-device location request is involved. Missing GPS never blocks Import.

The original accepted coordinates must remain accurate in the Catch. Do not log them, include them in exception messages, analytics or tracing tags, or use them as cache/log identifiers. The user must be able to review and remove the proposed location before confirmation. Import must not create different privacy semantics from Record Catch.

## 9. Simple location lookup recommendation

### Decision

Use Geoapify reverse geocoding behind a small server-side, provider-agnostic boundary for #222. Do not call a provider directly from Blazor and do not persist its label on Catch.

Conceptually, Application owns a contract such as `ResolveAsync(latitude, longitude, cancellationToken)` returning a result containing a display label and available locality, region and country fields. Infrastructure owns the Geoapify HTTP adapter. An authenticated API query exposes only the product result to Web. The UI depends on the FishingLogBook contract, not Geoapify's response.

There is no existing repository provider or dependency to reuse. Use `HttpClient`; a geocoding SDK is unnecessary.

### Provider comparison

| Provider | V1 fit | Key, limits and storage/terms |
|---|---|---|
| Geoapify | Recommended. Its reverse response exposes city/locality, county/state/region and country, and its OpenStreetMap-derived coverage is a reasonable low-cost starting point for rural/coastal labels. Quality still needs representative fishing-location sampling. | Requires an API key. The published free plan is 3,000 credits/day and 5 requests/second without a card; reverse lookup normally costs one credit. Results may be stored with required attribution. See [reverse geocoding](https://www.geoapify.com/reverse-geocoding-api/), [API fields](https://apidocs.geoapify.com/docs/geocoding/reverse-geocoding/) and [pricing](https://www.geoapify.com/pricing/). Recheck terms before release. |
| Public OSM Nominatim | Useful for a developer spike, not a dependable production backend. | The public service caps heavy use at one request/second, requires identification/attribution and caching, can change policy or withdraw access, and says not to submit personal/confidential data. See the [Nominatim usage policy](https://operations.osmfoundation.org/policies/nominatim/). |
| Google Geocoding | Mature response quality, but disproportionate for this small optional slice. | Requires an API key and billing. Storage/caching is restricted apart from place IDs and Google attribution/terms apply. See [usage and billing](https://developers.google.com/maps/documentation/geocoding/usage-and-billing) and [policies](https://developers.google.com/maps/documentation/geocoding/policies). |
| Mapbox Geocoding | Viable alternative if already adopted later, but its temporary/permanent result modes complicate this V1 decision. | Requires an access token. Temporary results cannot be cached; permanent geocoding has separate eligibility/storage conditions. See the [Geocoding API](https://docs.mapbox.com/api/search/geocoding/) and [temporary versus permanent geocoding](https://docs.mapbox.com/help/dive-deeper/understand-temporary-vs-permanent-geocoding/). |

Pricing and policies are external and may change. #222 must verify them when implementation starts and capture required attribution in the UI.

### Placement and behavior

Lookup belongs after #214 has extracted metadata and deduplicated near-identical coordinate requests, and before #216 renders review labels:

```text
select photos
  -> extract EXIF locally
  -> group/process metadata
  -> resolve unique GPS locations through the authenticated API
  -> show transient labels during review
```

It is not required by grouping (#215), which should use raw coordinates. Review can render immediately with a non-blocking “Looking up location…” state and update labels as results arrive.

It is also not an input to #220 Trip clustering. Trip suggestions use reviewed Catch date/time and accepted GPS coordinates. Labels may explain a cluster to the user, but a missing or failed lookup cannot change or block a Trip proposal.

Build a concise label from the most specific reliable fields without repetition: `locality, region, country`; if locality is absent, use `region, country`; if only country exists, show country. County may substitute for region where it is the provider's useful administrative field. Never guess a missing field or display a provider's verbose address blindly.

Lookup is optional enrichment. No GPS, no result, offline operation, timeout, cancellation, `429`, provider error or malformed response returns an unavailable status and the wizard continues. Preserve valid coordinates even when lookup fails.

Use a short per-request timeout (about three seconds). Permit at most one jittered retry for idempotent transient network/`408`/`5xx` failures within the overall budget. Do not immediately retry `429`; honor `Retry-After` only if it does not delay review, otherwise degrade gracefully. Never make confirmation wait for lookup.

### Cache and privacy

Deduplicate within the batch by a coordinate key rounded to four decimal places (roughly 11 metres of latitude). Keep the canonical coordinates unrounded for user review and persistence. Querying the rounded coordinate is sufficient for a place label, reduces disclosed precision slightly, and coalesces burst photographs. Add a small bounded in-memory server cache keyed by rounded coordinates and response language with a short TTL, subject to verified provider terms. Do not add a database table.

Historical fishing coordinates can reveal sensitive personal habits and locations. The Import introduction must disclose that optional place labels send rounded coordinates to a third-party geocoding provider and allow the user to opt out. The server keeps the API key in environment/secret configuration, never ships it to Wasm, forwards no user identity to the provider, and logs neither request coordinates nor provider URLs containing them. A privacy review must accept Geoapify as a processor/provider before production enablement. If that is not acceptable, disable labels; Import remains fully functional.

## 10. Duplicate strategy

| Strength | Signals | Behavior |
|---|---|---|
| Strong | Client-computed SHA-256 equality of the same canonical/original bytes within the batch | Block adding the exact duplicate to the same batch, with an explanation and override only if product review requires it. |
| Medium | Matching timestamp plus dimensions and byte size; a future perceptual-image signal | Warn during review; never silently reject. |
| Weak | Filename, timestamp alone, size alone, GPS proximity or membership in the same proposed group | Ignore for rejection; use only as explanatory context. |

Current persisted `CatchPhotograph` data has no content fingerprint, dimensions or original filename, so reliable cross-import detection is not possible without a deliberate persistence extension. #219 should decide whether to add a server-side fingerprint and how to handle pre-existing rows. Hash client-side and never log filenames or hashes unnecessarily.

#213 should reserve optional duplicate status/fingerprint state in the transient model, but it should not implement hashing. Weak signals must never cause silent data loss.

## 11. Persistence reuse plan

After the user confirms all Trip decisions, each new Trip proposal should be mapped to the existing `TripModel`, and each reviewed Catch to the existing `CatchModel` and `CatchPhotographModel`. A Catch selected for a new or existing Trip receives that Trip's ID before its first local save. A declined proposal leaves `TripId` null.

For newly created Trips, save the confirmed `TripModel` to the existing local Trip store first, then save its associated Catches locally with `TripId` already assigned. Repeat per independent cluster and save unassigned Catches normally. The existing dependency-aware synchronisation already synchronises a local Trip before dependent Catches, after which Catch metadata and photographs follow the current sequential path. Existing-Trip choices reuse the selected authoritative/local Trip ID and existing access rules.

This avoids persisting Catches and then requiring a second user-visible association pass. Reuse current Trip/Catch stores, synchronisers, authenticated commands/endpoints, presigned object upload, authorization, private-location semantics and authoritative rereads. No batch Import API is needed for V1.

If a photo upload fails after Catch creation, keep the Catch and photograph sync state locally and expose existing retry behavior. Server responses remain authoritative and are reconciled after successful upsert/upload. The client must never provide trusted owner identity.

## 12. Proposed wizard

1. **Batch details:** choose Fishing Method and Species using existing catalogue picker patterns.
2. **Choose photos:** multi-select, validate, show sequential metadata/thumbnail progress and allow cancellation/removal.
3. **Review proposed Catches:** show deterministic photo groups, thumbnails, caught-on state, accepted coordinates and optional transient place labels.
4. **Correct Catches:** edit date/time, method/species overrides and location; remove photos; use #217 split/merge controls.
5. **Review Trip suggestions:** show each independent cluster as a compact date/count/location summary. For each, offer Create Trip, Add to existing Trip, or Keep as separate catches. Allow removing Catches from a cluster, choosing another compatible Trip and declining all Trip creation.
6. **Confirm import:** show Trip/Catch/photo counts and relationships, save new Trips before dependent Catches, and report queued/synchronised/failure state.

Keep controls touch-sized and review cards compact. Reuse validation, date/location display and photo presentation conventions where they fit, but do not couple the new wizard to `RecordCatchEditor` state. Image recognition remains outside this milestone.

## 13. Test strategy

- **Unit:** metadata normalization and malformed inputs; explicit-offset/wall-clock/weak-fallback states; two-minute photo-grouping boundaries and determinism; Trip suggestions for same-date/nearby, same-date/distant, close-time/missing-GPS and multiple clusters; all Trip spatial/time threshold boundaries; deterministic ordering; ambiguous timezone/date exclusion; removal from a Trip proposal; transient-model invariants; label construction and coordinate normalization; duplicate classification; cancellation/cleanup orchestration.
- **bUnit:** wizard transitions, progress/error states, required confirmation, overrides, remove and split/merge; Trip suggestion display; Create Trip, choose existing Trip and No Trip decisions; remove a Catch from a suggestion; multiple suggested clusters; optional lookup failure/offline behavior; final relationship mapping.
- **Browser JS:** multi-file/blob registry, thumbnail/object URL creation and revocation, sequential retrieval, cancellation and cleanup. Memory assertions should be conservative because browser memory reporting is not portable.
- **Application/API:** authenticated lookup success/no-result/timeout/`429` with a fake provider handler; no provider payload leakage; existing Catch/photo command contracts and authorization.
- **Infrastructure:** provider response mapping/cache behavior and current Catch/photo repository coverage. Do not require a live provider in CI.
- **E2E:** authenticated one- and multi-proposal journeys; multiple historical Catches producing a Trip suggestion; user confirmation; local Trip-first save and dependency-aware sync; photo upload/retry; authoritative reload showing each Catch associated with the confirmed Trip. Use stubbed lookup responses.
- **Physical device:** the checklist below is a release gate for #214/#221; automated browser tests do not replace it.

Tests and production logs must use synthetic coordinates and must not print selected EXIF coordinates.

## 14. Real-device validation checklist

Run on at least one current Android installed PWA and one iPhone Home Screen PWA, recording OS/browser/device versions:

- install/open the PWA and select 1, 10 and the configured maximum photos;
- confirm multi-select UX and cancellation/back behavior;
- test JPEG, PNG, WebP and common HEIC-origin photos as the picker supplies them;
- compare original known EXIF date, offset, GPS and orientation with parsed results;
- test a photo whose metadata was stripped and one exported from a messaging/cloud app;
- observe thumbnail orientation, preparation time, memory pressure, background/resume and page disposal cleanup;
- confirm no current-location permission is requested;
- go offline after selection and confirm review continues with coordinates but no place label;
- test provider timeout/rate-limit behavior without blocking confirmation;
- confirm imported records, photo uploads, retry and authoritative reread after reconnect;
- confirm a multi-Catch historical Trip decision saves the Trip before its dependent Catches and survives offline/reconnect synchronization;
- visually check several known rural/coastal coordinates for useful locality/region/country labels.

## 15. Review of #213–#222 slice boundaries

| Issue | Boundary/dependency recommendation |
|---|---|
| #213 Transient model | Keep first. Include timestamp ambiguity, opaque blob/thumbnail lifecycle, optional location-lookup state and duplicate-signal placeholders; do not implement those services. |
| #214 Selection/metadata | Keep after #213. Own multi-select orchestration, browser blob/thumbnail lifetime and physical-device metadata evidence. Produce raw optional GPS. Do not own provider lookup. |
| #215 Grouping | Keep after #214. Implement the deterministic two-minute algorithm independently of reverse geocoding. |
| #222 Location lookup | Implement after #214 and in parallel with/after #215. Own the server abstraction/provider adapter, transient label, privacy disclosure, cache and failure behavior. It enables labels in #216 but never persistence. |
| #216 Review wizard | Depend on #215. Consume #222 labels when available, but render and remain usable when lookup is unavailable. Keep it UI-focused; split shell/selection from Catch proposal review only if estimation shows one PR is too broad. |
| #217 Split/merge | Keep after #216; modify only transient groups. |
| #220 Trip suggestions | Move into the first usable milestone after #217 and before #218. Own deterministic suggestions from reviewed Catches, existing-Trip choices and transient Trip decisions. It must not use raw photo groups or place-label strings as authoritative input. |
| #218 Persistence | Depend on #220. Reuse existing local Trip and Catch stores/synchronisers; save confirmed new Trips before Catches whose `TripId` is already assigned. |
| #219 Duplicates | Adjust scope/dependency: within-batch detection is useful after #214 and before final review; cross-import detection depends on #218 and may require persistence. Split those concerns if the ticket becomes broad. |
| #221 Hardening | Keep as the final cross-device/performance/accessibility/recovery gate, including Trip proposal and Trip-first persistence acceptance. |

The primary dependency chain is:

```text
#213 -> #214 -> #215 -> #216 -> #217 -> #220 -> #218 -> #221
          \-> #222 ------^ (review enrichment; never blocks Trip logic)
          \-> within-batch part of #219
#218 -> cross-import part of #219
```

## 16. Open questions and blockers

- Physical Android and iPhone picker/EXIF behavior is the main blocker to finalising #214's supported formats, batch cap and memory budget.
- Product/privacy review must approve sending rounded historical coordinates to Geoapify, the disclosure/opt-out wording and required attribution before #222 is enabled.
- #222 must sample known rural/coastal fishing locations and verify current Geoapify pricing, quotas, caching rights and terms at implementation time.
- Product should confirm the initial two-minute grouping threshold after trying representative personal photo sequences; the implementation remains configurable.
- Product should validate the proposed 5 km nearby, 25 km distant-veto, four-hour continuity and 18-hour span Trip thresholds against representative fishing sessions.
- #220 must confirm which existing completed Trip status/default is used for a historical Trip and whether `PlaceName` remains user-entered/confirmed; no title or place text should be invented.
- Existing-Trip suggestions need an agreed offline message when only cached/local Trips can be searched.
- #219 needs a decision on whether V1 covers only within-batch exact duplicates or adds persisted cross-import fingerprints.
- Product should confirm that losing an unconfirmed wizard on refresh is acceptable for V1; otherwise IndexedDB draft persistence becomes a separate later slice.

None of these questions requires a PostgreSQL Import schema, a persisted reverse-geocoded Catch label, an Import-only Trip model, a new EXIF dependency or a parallel persistence stack.
