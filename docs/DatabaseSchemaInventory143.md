# Issue 143 database schema inventory

This is the Phase 2 inventory for issue #143. It records the effective schema produced by
the active DbUp history before that history is rebaselined. Generated `bin/` and `obj/`
copies are excluded; the source scripts under `01_Tables`, `02_SeedData`, and `04_Scripts`
are authoritative.

## Effective table mapping

| Current table | Target table | Current columns -> target columns |
|---|---|---|
| `"SystemTest"` | `systemhealth` | `"Id"` -> `id`; `"Name"` -> `name`; `"CreatedOn"` -> `createdon` |
| `"User"` | `users` | `"Id"` -> `id`; `"Email"` -> `email`; `"CreatedOn"` -> `createdon`; `"OfflineAccessEnabled"` -> `offlineaccessenabled`; `"OfflineAccessEnabledAt"` -> `offlineaccessenabledat` |
| `"UserIdentity"` | `useridentities` | `"Id"` -> `id`; `"UserId"` -> `userid`; `"Provider"` -> `provider`; `"Subject"` -> `subject`; `"CreatedOn"` -> `createdon` |
| `"Profile"` | `profiles` | `"UserId"` -> `userid`; `"DisplayName"` -> `displayname`; `"PhotographId"` -> `photographid`; `"PhotographObjectKey"` -> `photographobjectkey`; `"PhotographContentType"` -> `photographcontenttype`; `"HomeRegion"` -> `homeregion`; `"PreferredWeightUnit"` -> `preferredweightunit`; `"PreferredLengthUnit"` -> `preferredlengthunit`; `"ShowDisplayName"` -> `showdisplayname`; `"ShowPhotograph"` -> `showphotograph`; `"ShowHomeRegion"` -> `showhomeregion`; `"ShowPreferredFishingMethods"` -> `showpreferredfishingmethods`; `"ShowPreferredSpecies"` -> `showpreferredspecies`; `"OnboardingCompletedOn"` -> `onboardingcompletedon`; `"CreatedOn"` -> `createdon`; `"UpdatedOn"` -> `updatedon` |
| `"Catch"` | `catches` | `"Id"` -> `id`; `"CaughtByUserId"`/`"AnglerUserId"`/`"UserId"` -> `caughtbyuserid`; `"RecordedByUserId"`/`"UserId"` -> `recordedbyuserid`; `"CaughtOn"` -> `caughton`; `"CreatedOn"` -> `createdon`; `"Latitude"` -> `latitude`; `"Longitude"` -> `longitude`; `"LocationAccuracyMetres"` -> `locationaccuracymetres`; `"LocationCapturedOn"` -> `locationcapturedon`; `"LocationSource"` -> `locationsource`; `"LocationVisibility"` -> `locationvisibility`; `"LocationConsentVersion"` -> `locationconsentversion`; `"SpeciesName"` -> `speciesname`; `"Weight"` -> `weight`; `"Length"` -> `length`; `"Method"` -> `method`; `"BaitOrLure"` -> `baitorlure`; `"Notes"` -> `notes`; `"TripId"` -> `tripid`. Legacy `"UserId"` and `"AnglerUserId"` have no target columns. |
| `"CatchPhotograph"` | `catchphotographs` | `"Id"` -> `id`; `"CatchId"` -> `catchid`; `"ContentType"` -> `contenttype` |
| `"PlatformCapability"` | `platformcapabilities` | `"Code"` -> `code`; `"CreatedOn"` -> `createdon` |
| `"UserPlatformCapability"` | `userplatformcapabilities` | `"UserId"` -> `userid`; `"CapabilityCode"` -> `capabilitycode`; `"CreatedOn"` -> `createdon` |
| `"FishingMethod"` | `fishingmethods` | `"Id"` -> `id`; `"Code"` -> `code`; `"Name"` -> `name`; `"CreatedOn"` -> `createdon` |
| `"Species"` | `species` | `"Id"` -> `id`; `"Code"` -> `code`; `"Name"` -> `name`; `"CreatedOn"` -> `createdon` |
| `"UserFishingMethodPreference"` | `userfishingmethodpreferences` | `"UserId"` -> `userid`; `"FishingMethodId"` -> `fishingmethodid`; `"IsDefault"` -> `isdefault`; `"CreatedOn"` -> `createdon` |
| `"UserFishingSpeciesPreference"` | `userfishingspeciespreferences` | `"UserId"` -> `userid`; `"FishingMethodId"` -> `fishingmethodid`; `"SpeciesId"` -> `speciesid`; `"IsDefault"` -> `isdefault`; `"CreatedOn"` -> `createdon` |
| `"Trip"` | `trips` | `"Id"` -> `id`; `"OwnerUserId"` -> `owneruserid`; `"Title"` -> `title`; `"PlaceName"` -> `placename`; `"Status"` -> `status`; `"StartedOn"` -> `startedon`; `"EndedOn"` -> `endedon`; `"Latitude"` -> `latitude`; `"Longitude"` -> `longitude`; `"LocationAccuracyMetres"` -> `locationaccuracymetres`; `"LocationCapturedOn"` -> `locationcapturedon`; `"LocationSource"` -> `locationsource`; `"LocationVisibility"` -> `locationvisibility`; `"LocationConsentVersion"` -> `locationconsentversion`; `"CreatedOn"` -> `createdon`; `"UpdatedOn"` -> `updatedon` |
| `"TripPhotograph"` | `tripphotographs` | `"Id"` -> `id`; `"TripId"` -> `tripid`; `"ObjectKey"` -> `objectkey`; `"ContentType"` -> `contenttype`; `"CapturedOn"` -> `capturedon`; `"AddedOn"` -> `addedon`; `"ContributedByUserId"` -> `contributedbyuserid`; `"CreatedOn"` -> `createdon`; `"UpdatedOn"` -> `updatedon` |
| `"TripNote"` | `tripnotes` | `"Id"` -> `id`; `"TripId"` -> `tripid`; `"CreatedByUserId"` -> `createdbyuserid`; `"Text"` -> `text`; `"RecordedOn"` -> `recordedon`; `"CreatedOn"` -> `createdon`; `"UpdatedOn"` -> `updatedon` |
| `"UserFishingLocationPreference"` | `userfishinglocationpreferences` | `"Id"` -> `id`; `"UserId"` -> `userid`; `"Name"` -> `name`; `"IsDefault"` -> `isdefault`; `"CreatedOn"` -> `createdon` |
| `"TripParticipant"` | `tripparticipants` | `"Id"` -> `id`; `"TripId"` -> `tripid`; `"UserId"` -> `userid`; `"Status"` -> `status`; `"InvitedByUserId"` -> `invitedbyuserid`; `"InvitedOn"` -> `invitedon`; `"RespondedOn"` -> `respondedon`; `"RemovedOn"` -> `removedon`; `"CreatedOn"` -> `createdon`; `"UpdatedOn"` -> `updatedon` |

`"TestCatch"` and `"TestCatchPhotograph"` are not current tables: the active history
creates them and then drops them in `202608191500_94_DropTestCatch.sql`. They have no target
tables. Their historical location columns and photograph columns are also excluded.

### Effective column definitions

`NN` means `NOT NULL`; `NULL` means nullable. `timestamptz` includes columns declared as
the equivalent `timestamp with time zone` spelling.

- `systemhealth`: `id uuid NN`, `name text NN`, `createdon timestamptz NN`.
- `users`: `id uuid NN`, `email text NN`, `createdon timestamptz NN`,
  `offlineaccessenabled boolean NN`, `offlineaccessenabledat timestamptz NULL`.
- `useridentities`: `id uuid NN`, `userid uuid NN`, `provider text NN`, `subject text NN`,
  `createdon timestamptz NN`.
- `profiles`: `userid uuid NN`, `displayname text NULL`, `photographid uuid NULL`,
  `photographobjectkey text NULL`, `photographcontenttype text NULL`, `homeregion text NULL`,
  `preferredweightunit integer NN`, `preferredlengthunit integer NN`,
  `showdisplayname boolean NN`, `showphotograph boolean NN`, `showhomeregion boolean NN`,
  `showpreferredfishingmethods boolean NN`, `showpreferredspecies boolean NN`,
  `onboardingcompletedon timestamptz NULL`, `createdon timestamptz NN`,
  `updatedon timestamptz NN`.
- `catches`: `id uuid NN`, `caughtbyuserid uuid NN` (target after validated coalescing),
  `recordedbyuserid uuid NN` (target after validated coalescing), `caughton timestamptz NN`,
  `createdon timestamptz NN`, `latitude double precision NULL`,
  `longitude double precision NULL`, `locationaccuracymetres double precision NULL`,
  `locationcapturedon timestamptz NULL`, `locationsource text NULL`,
  `locationvisibility text NULL`, `locationconsentversion text NULL`,
  `speciesname text NULL`, `weight numeric(8,3) NULL`, `length numeric(8,2) NULL`,
  `method text NULL`, `baitorlure text NULL`, `notes text NULL`, `tripid uuid NULL`.
- `catchphotographs`: `id uuid NN`, `catchid uuid NN`, `contenttype text NN`.
- `platformcapabilities`: `code text NN`, `createdon timestamptz NN`.
- `userplatformcapabilities`: `userid uuid NN`, `capabilitycode text NN`,
  `createdon timestamptz NN`.
- `fishingmethods` and `species`: `id uuid NN`, `code text NN`, `name text NN`,
  `createdon timestamptz NN`.
- `userfishingmethodpreferences`: `userid uuid NN`, `fishingmethodid uuid NN`,
  `isdefault boolean NN`, `createdon timestamptz NN`.
- `userfishingspeciespreferences`: `userid uuid NN`, `fishingmethodid uuid NN`,
  `speciesid uuid NN`, `isdefault boolean NN`, `createdon timestamptz NN`.
- `trips`: `id uuid NN`, `owneruserid uuid NN`, `title text NULL`, `placename text NULL`,
  `status text NN`, `startedon timestamptz NN`, `endedon timestamptz NULL`,
  `latitude double precision NULL`, `longitude double precision NULL`,
  `locationaccuracymetres double precision NULL`, `locationcapturedon timestamptz NULL`,
  `locationsource text NULL`, `locationvisibility text NULL`,
  `locationconsentversion text NULL`, `createdon timestamptz NN`, `updatedon timestamptz NN`.
- `tripphotographs`: `id uuid NN`, `tripid uuid NN`, `objectkey text NN`,
  `contenttype text NN`, `capturedon timestamptz NULL`, `addedon timestamptz NN`,
  `contributedbyuserid uuid NN`, `createdon timestamptz NN`, `updatedon timestamptz NN`.
- `tripnotes`: `id uuid NN`, `tripid uuid NN`, `createdbyuserid uuid NN`, `text text NN`,
  `recordedon timestamptz NN`, `createdon timestamptz NN`, `updatedon timestamptz NN`.
- `userfishinglocationpreferences`: `id uuid NN`, `userid uuid NN`, `name text NN`,
  `isdefault boolean NN`, `createdon timestamptz NN`.
- `tripparticipants`: `id uuid NN`, `tripid uuid NN`, `userid uuid NN`, `status text NN`,
  `invitedbyuserid uuid NN`, `invitedon timestamptz NN`, `respondedon timestamptz NULL`,
  `removedon timestamptz NULL`, `createdon timestamptz NN`, `updatedon timestamptz NN`.

## Keys, constraints, indexes, defaults, and identity

All foreign keys use PostgreSQL's default `NO ACTION` for both update and delete unless
stated otherwise. All UUID primary keys are supplied by application/seed SQL; no table uses
`serial`, `bigserial`, `GENERATED ... AS IDENTITY`, an owned sequence, or a UUID column
default. DbUp's `SchemaVersions` journal is infrastructure metadata, not an application
table in this inventory.

| Target table | Primary/unique keys and indexes | Foreign keys | Checks | Defaults |
|---|---|---|---|---|
| `systemhealth` | `pksystemhealth (id)` | none | none | `createdon = now()` |
| `users` | `pkusers (id)` | none | none | `createdon = now()`; `offlineaccessenabled = false` |
| `useridentities` | `pkuseridentities (id)`; `uxuseridentitiesprovidersubject (provider, subject)`; `ixuseridentitiesuserid (userid)` | `userid -> users.id` | none | `createdon = now()` |
| `profiles` | `pkprofiles (userid)` | `userid -> users.id` | `preferredweightunit in (0,1)`; `preferredlengthunit in (0,1)` | booleans retain current true/false defaults; unit preferences `0`; timestamps `now()` |
| `catches` | `pkcatches (id)`; `ixcatchescaughtbyuserid`; `ixcatchestripid` | `caughtbyuserid -> users.id`; `recordedbyuserid -> users.id`; `tripid -> trips.id ON DELETE SET NULL` | location coherence; allowed visibility; weight `(0,1000]`; length `(0,1000]` | `createdon = now()` |
| `catchphotographs` | `pkcatchphotographs (id)`; `ixcatchphotographscatchid` | `catchid -> catches.id` | none | none |
| `platformcapabilities` | `pkplatformcapabilities (code)` | none | none | `createdon = now()` |
| `userplatformcapabilities` | `pkuserplatformcapabilities (userid, capabilitycode)` | `userid -> users.id`; `capabilitycode -> platformcapabilities.code` | none | `createdon = now()` |
| `fishingmethods` | `pkfishingmethods (id)`; `uxfishingmethodscode (code)` | none | none | `createdon = now()` |
| `species` | `pkspecies (id)`; `uxspeciescode (code)` | none | none | `createdon = now()` |
| `userfishingmethodpreferences` | `pkuserfishingmethodpreferences (userid, fishingmethodid)`; partial `uxuserfishingmethodpreferencesdefault (userid) WHERE isdefault` | `userid -> users.id`; `fishingmethodid -> fishingmethods.id` | none | `isdefault = false`; `createdon = now()` |
| `userfishingspeciespreferences` | `pkuserfishingspeciespreferences (userid, fishingmethodid, speciesid)`; partial `uxuserfishingspeciespreferencesdefault (userid, fishingmethodid) WHERE isdefault` | composite `(userid, fishingmethodid) -> userfishingmethodpreferences`; `speciesid -> species.id` | none | `isdefault = false`; `createdon = now()` |
| `trips` | `pktrips (id)`; `ixtripsowneruserid`; partial `uxtripsowneractive (owneruserid) WHERE status = 'Active'` | `owneruserid -> users.id` | status allowed; end after start; active has no end; location coherence; allowed visibility | `createdon/updatedon = now()` |
| `tripphotographs` | `pktripphotographs (id)`; `ixtripphotographstripid`; `uxtripphotographsobjectkey`; `ixtripphotographscontributedbyuserid` | `tripid -> trips.id`; `contributedbyuserid -> users.id` | none | `createdon/updatedon = now()` |
| `tripnotes` | `pktripnotes (id)`; `ixtripnotestripid`; `ixtripnotestriprecordedon (tripid, recordedon)` | `tripid -> trips.id`; `createdbyuserid -> users.id` | none | `createdon/updatedon = now()` |
| `userfishinglocationpreferences` | `pkuserfishinglocationpreferences (id)`; `ixuserfishinglocationpreferencesuserid`; expression unique `uxuserfishinglocationpreferencesname (userid, lower(btrim(name)))`; partial `uxuserfishinglocationpreferencesdefault (userid) WHERE isdefault` | `userid -> users.id` | trimmed name non-empty and length <= 160 | `isdefault = false`; `createdon = now()` |
| `tripparticipants` | `pktripparticipants (id)`; `uxtripparticipantstripuser (tripid, userid)`; `ixtripparticipantsuserstatus (userid, status)` | `tripid -> trips.id`; `userid -> users.id`; `invitedbyuserid -> users.id` | status allowed; response after invitation; pending has no response; not self-invited; removed participant was accepted | `createdon/updatedon = now()` |

The target `catches` table intentionally omits the legacy `userid` and `angleruserid`
columns and their foreign keys/index. `caughtbyuserid` and `recordedbyuserid` are populated
by the issue-mandated coalescing rules. Since legacy `"UserId"` is non-null, both target
values can be validated as non-null during copy; the target baseline should make both
columns non-null after that validation.

## Seed and one-off history

- `systemhealth`: one generated UUID row named `FishingLogBook database online`.
- `platformcapabilities`: `Guide`, `FishingVenueManager`, `CompetitionOrganiser`, and
  `Administrator`.
- `fishingmethods`: five stable UUID/code/name rows (`Fly`, `Spinning`, `Bait`, `Lure`,
  and `Trolling`).
- `species`: twelve stable UUID/code/name rows from `BrownTrout` through `Grayling`.
- Current `04_Scripts` effects: remove TestCatch tables; ensure a profile/onboarding value
  for every existing user; backfill trip photograph contributor from trip owner and make
  it non-null; backfill blank profile display names from user email.
- The old profile fishing arrays were deliberately discarded by issue #90 and are not part
  of the effective schema or target baseline.

## Persistence-reference inventory

Production SQL is confined to these Dapper repositories:

- `CatchRepository.cs`
- `FishingCatalogueRepository.cs`
- `FishingLocationPreferenceRepository.cs`
- `FishingPreferenceRepository.cs`
- `OfflineAccessPreferenceRepository.cs`
- `ProfileRepository.cs`
- `SystemRepository.cs`
- `TripNoteRepository.cs`
- `TripParticipantRepository.cs`
- `TripPhotographRepository.cs`
- `TripRepository.cs`
- `UserIdentityRepository.cs`
- `UserPlatformCapabilityRepository.cs`

No Application service contains database SQL. Application services call repository
contracts; Web offline stores use IndexedDB JavaScript rather than PostgreSQL.

Live PostgreSQL references also exist throughout
`tests/FishingLogBook.Infrastructure.Tests/Repositories/Repositories/`, particularly the
shared `Base*RepositoryTest` seed/setup methods and tests containing direct verification
SQL. Migration/schema assertions are under
`tests/FishingLogBook.Infrastructure.Tests/Repositories/Migrations/SchemaTests/`, and the
container/migration bootstrap is
`tests/FishingLogBook.Infrastructure.Tests/Repositories/TestSupport/PostgresFixture.cs`.
These all require conversion with the repositories.

Other active references requiring semantic or executable-SQL updates are:

- `FishingLogBook.Domain/SystemStatus/SystemTestRecord.cs`, its Application repository
  contract/service, `SystemRepository.cs`, API/Application tests, and the common
  `SystemTestRecordBuilder.cs` (`SystemTest` -> `SystemHealth`).
- `README.md` and `BUILD.md` health/status descriptions.
- `tests/FishingLogBook.Infrastructure.Tests/Repositories/Migrations/SchemaTests/WhenTestingSchema.cs`
  and `src/FishingLogBook.Web/wwwroot/js/storage/schema-safety.test.js`, which assert the
  absence of legacy TestCatch concepts.
- All active source migrations and seeds listed at the start of this document.

No separate E2E database cleanup SQL, operational SQL folder, stored routines, database
views, triggers, or application-owned sequences were found. `03_Routines` is absent. The
only developer/manual SQL examples found in current guidance are the unquoted examples in
`.claude/rules/database.md` and `BUILD.md`.

## Ambiguity and data-dependent gates

No unresolved repository-level schema dependency was found. The following are mandatory
data-dependent gates for the later existing-database migration and cannot be proven from
source inventory alone:

1. Catch rows must be checked for disagreement among `"CaughtByUserId"`,
   `"AnglerUserId"`, and `"UserId"` before copying. Any unexplained disagreement stops the
   migration.
2. The copy must prove both coalesced Catch provenance values are non-null and reference
   existing users before cleanup.
3. Source/destination row counts, all foreign keys, unique/expression/partial-index
   invariants, and relationship counts must be validated against representative populated
   data.
4. `"Profile"."DisplayName"` remains nullable in the effective schema even though a
   one-off script backfilled existing blank values; the target must preserve that
   nullability unless a separate product requirement changes it.
