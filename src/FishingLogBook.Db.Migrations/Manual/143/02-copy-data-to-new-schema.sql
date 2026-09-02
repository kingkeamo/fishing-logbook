-- FLB#143: copy the existing quoted PascalCase application data into the
-- lowercase schema created by the rebaselined DbUp migrations.
--
-- Operational prerequisites:
--   1. Stop all application writes and enter a maintenance window.
--   2. Take and verify a database backup.
--   3. Run 01-reset-schema-versions.sql and the normal DbUp runner so that
--      the old quoted and new lowercase schemas coexist in this database.
--   4. Run this entire file as one script. Do not run selected statements.
--
-- Expected lowercase destination state immediately before this script runs:
--   systemhealth contains the canonical baseline seed;
--   platformcapabilities, fishingmethods, and species contain their baseline
--   reference seeds; and every other lowercase application/business table is
--   empty. The script first validates the baseline reference codes, then
--   replaces platformcapabilities, fishingmethods, and species with the exact
--   current rows from the old schema. The baseline systemhealth row is retained;
--   the historical old SystemTest seed is not copied.
--
-- Rerun policy:
--   This script refuses to run if any destination business table contains
--   rows. If a prior attempt committed, reset/recreate the lowercase schema
--   before running it again.
--
-- This script copies only. It never changes or removes old quoted data and it
-- does not contain the later destructive old-schema cleanup.

begin;

-- Fail before touching destination data if either schema is incomplete.
do $$
declare
    required_table text;
begin
    foreach required_table in array array[
        '"User"', '"UserIdentity"', '"Profile"',
        '"PlatformCapability"', '"UserPlatformCapability"', '"FishingMethod"',
        '"Species"', '"UserFishingMethodPreference"',
        '"UserFishingSpeciesPreference"', '"Trip"', '"TripParticipant"',
        '"TripPhotograph"', '"TripNote"', '"UserFishingLocationPreference"',
        '"Catch"', '"CatchPhotograph"',
        'systemhealth', 'users', 'useridentities', 'profiles',
        'platformcapabilities', 'userplatformcapabilities', 'fishingmethods',
        'species', 'userfishingmethodpreferences',
        'userfishingspeciespreferences', 'trips', 'tripparticipants',
        'tripphotographs', 'tripnotes', 'userfishinglocationpreferences',
        'catches', 'catchphotographs'
    ]
    loop
        if to_regclass(required_table) is null then
            raise exception 'FLB#143 copy blocked: required table % does not exist.', required_table;
        end if;
    end loop;
end $$;

-- A controlled rerun must start with empty destination business tables.
do $$
begin
    if exists (select 1 from users)
        or exists (select 1 from useridentities)
        or exists (select 1 from profiles)
        or exists (select 1 from userplatformcapabilities)
        or exists (select 1 from userfishingmethodpreferences)
        or exists (select 1 from userfishingspeciespreferences)
        or exists (select 1 from trips)
        or exists (select 1 from tripparticipants)
        or exists (select 1 from tripphotographs)
        or exists (select 1 from tripnotes)
        or exists (select 1 from userfishinglocationpreferences)
        or exists (select 1 from catches)
        or exists (select 1 from catchphotographs)
    then
        raise exception 'FLB#143 copy blocked: destination business tables are not empty. Reset the lowercase schema before retrying.';
    end if;
end $$;

-- The baseline codes must describe the same reference entries used by the old
-- schema. Catalogue IDs, names, and timestamps are copied from the old schema
-- before any dependent preference rows are copied.
do $$
begin
    if (select count(*) from systemhealth) <> 1 then
        raise exception 'FLB#143 copy blocked: the new baseline must contain exactly one canonical systemhealth row.';
    end if;

    if exists (
        select old."Code"
        from "PlatformCapability" old
        full join platformcapabilities new on new.code = old."Code"
        where old."Code" is null or new.code is null
    ) then
        raise exception 'FLB#143 copy blocked: platform capability codes differ between old data and the new baseline.';
    end if;

    if exists (
        select coalesce(old."Code", new.code)
        from "FishingMethod" old
        full join fishingmethods new on new.code = old."Code"
        where old."Code" is null
           or new.code is null
    ) then
        raise exception 'FLB#143 copy blocked: fishing method codes differ between old data and the new baseline.';
    end if;

    if exists (
        select coalesce(old."Code", new.code)
        from "Species" old
        full join species new on new.code = old."Code"
        where old."Code" is null
           or new.code is null
    ) then
        raise exception 'FLB#143 copy blocked: species codes differ between old data and the new baseline.';
    end if;
end $$;

-- Validate the source conditions required by the target constraints.
do $$
begin
    if exists (
        select 1 from "Catch"
        where coalesce("CaughtByUserId", "AnglerUserId", "UserId") is null
           or coalesce("RecordedByUserId", "UserId") is null
    ) then
        raise exception 'FLB#143 copy blocked: a Catch has no canonical caught-by or recorded-by user.';
    end if;

    if exists (
        select 1
        from "Catch" c
        left join "User" u on u."Id" = coalesce(c."CaughtByUserId", c."AnglerUserId", c."UserId")
        where u."Id" is null
    ) or exists (
        select 1
        from "Catch" c
        left join "User" u on u."Id" = coalesce(c."RecordedByUserId", c."UserId")
        where u."Id" is null
    ) then
        raise exception 'FLB#143 copy blocked: a canonical Catch user does not exist in old User.';
    end if;

    if exists (
        select 1 from "Profile"
        where "PreferredWeightUnit" not in (0, 1)
           or "PreferredLengthUnit" not in (0, 1)
    ) then
        raise exception 'FLB#143 copy blocked: a Profile unit preference is outside the target range.';
    end if;

    if exists (select 1 from "TripPhotograph" where "ContributedByUserId" is null) then
        raise exception 'FLB#143 copy blocked: a TripPhotograph has no contributor after the historical backfill.';
    end if;

    if exists (
        select 1 from "Catch" c
        left join "Trip" t on t."Id" = c."TripId"
        where c."TripId" is not null and t."Id" is null
    ) then
        raise exception 'FLB#143 copy blocked: a Catch references a missing Trip.';
    end if;

    if exists (
        select 1 from "UserFishingMethodPreference" p
        left join "FishingMethod" m on m."Id" = p."FishingMethodId"
        where m."Id" is null
    ) or exists (
        select 1 from "UserFishingSpeciesPreference" p
        left join "Species" s on s."Id" = p."SpeciesId"
        where s."Id" is null
    ) then
        raise exception 'FLB#143 copy blocked: a preference references missing catalogue data.';
    end if;

    if exists (
        select 1 from "UserIdentity" i
        left join "User" u on u."Id" = i."UserId" where u."Id" is null
    ) or exists (
        select 1 from "Profile" p
        left join "User" u on u."Id" = p."UserId" where u."Id" is null
    ) or exists (
        select 1 from "UserPlatformCapability" p
        left join "User" u on u."Id" = p."UserId"
        left join "PlatformCapability" c on c."Code" = p."CapabilityCode"
        where u."Id" is null or c."Code" is null
    ) or exists (
        select 1 from "UserFishingMethodPreference" p
        left join "User" u on u."Id" = p."UserId"
        where u."Id" is null
    ) or exists (
        select 1 from "UserFishingSpeciesPreference" p
        left join "UserFishingMethodPreference" m
            on m."UserId" = p."UserId" and m."FishingMethodId" = p."FishingMethodId"
        where m."UserId" is null
    ) then
        raise exception 'FLB#143 copy blocked: an old user/reference relationship is orphaned.';
    end if;

    if exists (
        select 1 from "Trip" t
        left join "User" u on u."Id" = t."OwnerUserId" where u."Id" is null
    ) or exists (
        select 1 from "TripParticipant" p
        left join "Trip" t on t."Id" = p."TripId"
        left join "User" u on u."Id" = p."UserId"
        left join "User" inviter on inviter."Id" = p."InvitedByUserId"
        where t."Id" is null or u."Id" is null or inviter."Id" is null
    ) or exists (
        select 1 from "TripPhotograph" p
        left join "Trip" t on t."Id" = p."TripId"
        left join "User" u on u."Id" = p."ContributedByUserId"
        where t."Id" is null or u."Id" is null
    ) or exists (
        select 1 from "TripNote" n
        left join "Trip" t on t."Id" = n."TripId"
        left join "User" u on u."Id" = n."CreatedByUserId"
        where t."Id" is null or u."Id" is null
    ) or exists (
        select 1 from "UserFishingLocationPreference" p
        left join "User" u on u."Id" = p."UserId" where u."Id" is null
    ) or exists (
        select 1 from "CatchPhotograph" p
        left join "Catch" c on c."Id" = p."CatchId" where c."Id" is null
    ) then
        raise exception 'FLB#143 copy blocked: an old Trip/location/photograph relationship is orphaned.';
    end if;

    if exists (
        select "OwnerUserId" from "Trip" where "Status" = 'Active'
        group by "OwnerUserId" having count(*) > 1
    ) then
        raise exception 'FLB#143 copy blocked: a user owns more than one active Trip.';
    end if;

    if exists (
        select "UserId" from "UserFishingMethodPreference" where "IsDefault"
        group by "UserId" having count(*) > 1
    ) or exists (
        select "UserId", "FishingMethodId" from "UserFishingSpeciesPreference" where "IsDefault"
        group by "UserId", "FishingMethodId" having count(*) > 1
    ) or exists (
        select "UserId" from "UserFishingLocationPreference" where "IsDefault"
        group by "UserId" having count(*) > 1
    ) then
        raise exception 'FLB#143 copy blocked: source default-preference uniqueness is invalid.';
    end if;

    if exists (
        select "UserId", lower(btrim("Name"))
        from "UserFishingLocationPreference"
        group by "UserId", lower(btrim("Name")) having count(*) > 1
    ) then
        raise exception 'FLB#143 copy blocked: source fishing-location names are not unique after normalization.';
    end if;

    if exists (
        select "TripId", "UserId" from "TripParticipant"
        group by "TripId", "UserId" having count(*) > 1
    ) then
        raise exception 'FLB#143 copy blocked: duplicate Trip participants exist.';
    end if;
end $$;

-- The old SystemTest row is a historical health seed, not business data. Keep
-- the single canonical systemhealth row created by the new baseline.

-- Baseline reference rows are replaced by the authoritative current source rows.
delete from platformcapabilities;
delete from fishingmethods;
delete from species;

-- FK-safe copy order begins with roots and reference data.
insert into users (
    id, email, createdon, offlineaccessenabled, offlineaccessenabledat
)
select
    "Id", "Email", "CreatedOn", "OfflineAccessEnabled", "OfflineAccessEnabledAt"
from "User";

insert into platformcapabilities (code, createdon)
select "Code", "CreatedOn"
from "PlatformCapability";

insert into fishingmethods (id, code, name, createdon)
select "Id", "Code", "Name", "CreatedOn"
from "FishingMethod";

insert into species (id, code, name, createdon)
select "Id", "Code", "Name", "CreatedOn"
from "Species";

insert into useridentities (id, userid, provider, subject, createdon)
select "Id", "UserId", "Provider", "Subject", "CreatedOn"
from "UserIdentity";

insert into profiles (
    userid, displayname, photographid, photographobjectkey,
    photographcontenttype, homeregion, preferredweightunit,
    preferredlengthunit, showdisplayname, showphotograph, showhomeregion,
    showpreferredfishingmethods, showpreferredspecies,
    onboardingcompletedon, createdon, updatedon
)
select
    "UserId", "DisplayName", "PhotographId", "PhotographObjectKey",
    "PhotographContentType", "HomeRegion", "PreferredWeightUnit",
    "PreferredLengthUnit", "ShowDisplayName", "ShowPhotograph", "ShowHomeRegion",
    "ShowPreferredFishingMethods", "ShowPreferredSpecies",
    "OnboardingCompletedOn", "CreatedOn", "UpdatedOn"
from "Profile";

insert into userplatformcapabilities (userid, capabilitycode, createdon)
select "UserId", "CapabilityCode", "CreatedOn"
from "UserPlatformCapability";

insert into userfishingmethodpreferences (
    userid, fishingmethodid, isdefault, createdon
)
select "UserId", "FishingMethodId", "IsDefault", "CreatedOn"
from "UserFishingMethodPreference";

insert into userfishingspeciespreferences (
    userid, fishingmethodid, speciesid, isdefault, createdon
)
select "UserId", "FishingMethodId", "SpeciesId", "IsDefault", "CreatedOn"
from "UserFishingSpeciesPreference";

insert into trips (
    id, owneruserid, title, placename, status, startedon, endedon,
    latitude, longitude, locationaccuracymetres, locationcapturedon,
    locationsource, locationvisibility, locationconsentversion,
    createdon, updatedon
)
select
    "Id", "OwnerUserId", "Title", "PlaceName", "Status", "StartedOn", "EndedOn",
    "Latitude", "Longitude", "LocationAccuracyMetres", "LocationCapturedOn",
    "LocationSource", "LocationVisibility", "LocationConsentVersion",
    "CreatedOn", "UpdatedOn"
from "Trip";

insert into tripparticipants (
    id, tripid, userid, status, invitedbyuserid, invitedon,
    respondedon, removedon, createdon, updatedon
)
select
    "Id", "TripId", "UserId", "Status", "InvitedByUserId", "InvitedOn",
    "RespondedOn", "RemovedOn", "CreatedOn", "UpdatedOn"
from "TripParticipant";

insert into tripphotographs (
    id, tripid, objectkey, contenttype, capturedon, addedon,
    contributedbyuserid, createdon, updatedon
)
select
    "Id", "TripId", "ObjectKey", "ContentType", "CapturedOn", "AddedOn",
    "ContributedByUserId", "CreatedOn", "UpdatedOn"
from "TripPhotograph";

insert into tripnotes (
    id, tripid, createdbyuserid, text, recordedon, createdon, updatedon
)
select
    "Id", "TripId", "CreatedByUserId", "Text", "RecordedOn", "CreatedOn", "UpdatedOn"
from "TripNote";

insert into userfishinglocationpreferences (
    id, userid, name, isdefault, createdon
)
select "Id", "UserId", "Name", "IsDefault", "CreatedOn"
from "UserFishingLocationPreference";

insert into catches (
    id, caughtbyuserid, recordedbyuserid, caughton, createdon,
    latitude, longitude, locationaccuracymetres, locationcapturedon,
    locationsource, locationvisibility, locationconsentversion,
    speciesname, weight, length, method, baitorlure, notes, tripid
)
select
    "Id",
    coalesce("CaughtByUserId", "AnglerUserId", "UserId"),
    coalesce("RecordedByUserId", "UserId"),
    "CaughtOn", "CreatedOn",
    "Latitude", "Longitude", "LocationAccuracyMetres", "LocationCapturedOn",
    "LocationSource", "LocationVisibility", "LocationConsentVersion",
    "SpeciesName", "Weight", "Length", "Method", "BaitOrLure", "Notes", "TripId"
from "Catch";

insert into catchphotographs (id, catchid, contenttype)
select "Id", "CatchId", "ContentType"
from "CatchPhotograph";

-- Hard post-copy validation. Any mismatch aborts and rolls back every insert.
do $$
begin
    if (select count(*) from "User") <> (select count(*) from users)
        or exists (select "Id" from "User" except select id from users)
    then raise exception 'FLB#143 validation failed: users rows differ.'; end if;

    if (select count(*) from "UserIdentity") <> (select count(*) from useridentities)
        or exists (select "Id" from "UserIdentity" except select id from useridentities)
    then raise exception 'FLB#143 validation failed: useridentities rows differ.'; end if;

    if (select count(*) from "Profile") <> (select count(*) from profiles)
        or exists (select "UserId" from "Profile" except select userid from profiles)
    then raise exception 'FLB#143 validation failed: profiles rows differ.'; end if;

    if (select count(*) from "PlatformCapability") <> (select count(*) from platformcapabilities)
        or exists (
            select 1
            from "PlatformCapability" old
            full join platformcapabilities new on new.code = old."Code"
            where old."Code" is null
                or new.code is null
                or old."Code" is distinct from new.code
                or old."CreatedOn" is distinct from new.createdon
        )
    then raise exception 'FLB#143 validation failed: platformcapabilities rows differ.'; end if;

    if (select count(*) from "UserPlatformCapability") <> (select count(*) from userplatformcapabilities)
        or exists (
            select "UserId", "CapabilityCode" from "UserPlatformCapability"
            except select userid, capabilitycode from userplatformcapabilities
        )
    then raise exception 'FLB#143 validation failed: userplatformcapabilities rows differ.'; end if;

    if (select count(*) from "FishingMethod") <> (select count(*) from fishingmethods)
        or exists (
            select 1
            from "FishingMethod" old
            full join fishingmethods new on new.id = old."Id"
            where old."Id" is null
                or new.id is null
                or old."Id" is distinct from new.id
                or old."Code" is distinct from new.code
                or old."Name" is distinct from new.name
                or old."CreatedOn" is distinct from new.createdon
        )
    then raise exception 'FLB#143 validation failed: fishingmethods rows differ.'; end if;

    if (select count(*) from "Species") <> (select count(*) from species)
        or exists (
            select 1
            from "Species" old
            full join species new on new.id = old."Id"
            where old."Id" is null
                or new.id is null
                or old."Id" is distinct from new.id
                or old."Code" is distinct from new.code
                or old."Name" is distinct from new.name
                or old."CreatedOn" is distinct from new.createdon
        )
    then raise exception 'FLB#143 validation failed: species rows differ.'; end if;

    if (select count(*) from "UserFishingMethodPreference") <> (select count(*) from userfishingmethodpreferences)
        or exists (
            select "UserId", "FishingMethodId" from "UserFishingMethodPreference"
            except select userid, fishingmethodid from userfishingmethodpreferences
        )
    then raise exception 'FLB#143 validation failed: userfishingmethodpreferences rows differ.'; end if;

    if (select count(*) from "UserFishingSpeciesPreference") <> (select count(*) from userfishingspeciespreferences)
        or exists (
            select "UserId", "FishingMethodId", "SpeciesId" from "UserFishingSpeciesPreference"
            except select userid, fishingmethodid, speciesid from userfishingspeciespreferences
        )
    then raise exception 'FLB#143 validation failed: userfishingspeciespreferences rows differ.'; end if;

    if (select count(*) from "Trip") <> (select count(*) from trips)
        or exists (select "Id" from "Trip" except select id from trips)
    then raise exception 'FLB#143 validation failed: trips rows differ.'; end if;

    if (select count(*) from "TripParticipant") <> (select count(*) from tripparticipants)
        or exists (select "Id" from "TripParticipant" except select id from tripparticipants)
    then raise exception 'FLB#143 validation failed: tripparticipants rows differ.'; end if;

    if (select count(*) from "TripPhotograph") <> (select count(*) from tripphotographs)
        or exists (select "Id" from "TripPhotograph" except select id from tripphotographs)
    then raise exception 'FLB#143 validation failed: tripphotographs rows differ.'; end if;

    if (select count(*) from "TripNote") <> (select count(*) from tripnotes)
        or exists (select "Id" from "TripNote" except select id from tripnotes)
    then raise exception 'FLB#143 validation failed: tripnotes rows differ.'; end if;

    if (select count(*) from "UserFishingLocationPreference") <> (select count(*) from userfishinglocationpreferences)
        or exists (select "Id" from "UserFishingLocationPreference" except select id from userfishinglocationpreferences)
    then raise exception 'FLB#143 validation failed: userfishinglocationpreferences rows differ.'; end if;

    if (select count(*) from "Catch") <> (select count(*) from catches)
        or exists (select "Id" from "Catch" except select id from catches)
    then raise exception 'FLB#143 validation failed: catches rows differ.'; end if;

    if (select count(*) from "CatchPhotograph") <> (select count(*) from catchphotographs)
        or exists (select "Id" from "CatchPhotograph" except select id from catchphotographs)
    then raise exception 'FLB#143 validation failed: catchphotographs rows differ.'; end if;
end $$;

-- Validate canonical Catch provenance and important relationships directly.
do $$
begin
    if exists (
        select 1
        from "Catch" old
        join catches new on new.id = old."Id"
        where new.caughtbyuserid is distinct from
                  coalesce(old."CaughtByUserId", old."AnglerUserId", old."UserId")
           or new.recordedbyuserid is distinct from
                  coalesce(old."RecordedByUserId", old."UserId")
    ) then
        raise exception 'FLB#143 validation failed: Catch provenance was not copied canonically.';
    end if;

    if exists (select 1 from useridentities i left join users u on u.id = i.userid where u.id is null)
        or exists (select 1 from profiles p left join users u on u.id = p.userid where u.id is null)
        or exists (
            select 1 from userplatformcapabilities p
            left join users u on u.id = p.userid
            left join platformcapabilities c on c.code = p.capabilitycode
            where u.id is null or c.code is null
        )
        or exists (
            select 1 from userfishingmethodpreferences p
            left join users u on u.id = p.userid
            left join fishingmethods m on m.id = p.fishingmethodid
            where u.id is null or m.id is null
        )
        or exists (
            select 1 from userfishingspeciespreferences p
            left join userfishingmethodpreferences m
                on m.userid = p.userid and m.fishingmethodid = p.fishingmethodid
            left join species s on s.id = p.speciesid
            where m.userid is null or s.id is null
        )
    then
        raise exception 'FLB#143 validation failed: a user/reference relationship is orphaned.';
    end if;

    if exists (select 1 from trips t left join users u on u.id = t.owneruserid where u.id is null)
        or exists (
            select 1 from tripparticipants p
            left join trips t on t.id = p.tripid
            left join users u on u.id = p.userid
            left join users inviter on inviter.id = p.invitedbyuserid
            where t.id is null or u.id is null or inviter.id is null
        )
        or exists (
            select 1 from tripphotographs p
            left join trips t on t.id = p.tripid
            left join users u on u.id = p.contributedbyuserid
            where t.id is null or u.id is null
        )
        or exists (
            select 1 from tripnotes n
            left join trips t on t.id = n.tripid
            left join users u on u.id = n.createdbyuserid
            where t.id is null or u.id is null
        )
        or exists (
            select 1 from userfishinglocationpreferences p
            left join users u on u.id = p.userid where u.id is null
        )
    then
        raise exception 'FLB#143 validation failed: a Trip/location relationship is orphaned.';
    end if;

    if exists (
        select 1 from catches c
        left join users caughtby on caughtby.id = c.caughtbyuserid
        left join users recordedby on recordedby.id = c.recordedbyuserid
        left join trips t on t.id = c.tripid
        where caughtby.id is null or recordedby.id is null
           or (c.tripid is not null and t.id is null)
    ) or exists (
        select 1 from catchphotographs p
        left join catches c on c.id = p.catchid where c.id is null
    ) then
        raise exception 'FLB#143 validation failed: a Catch/photograph relationship is orphaned.';
    end if;

    if exists (select 1 from tripphotographs where contributedbyuserid is null)
        or exists (select 1 from catches where caughtbyuserid is null or recordedbyuserid is null)
    then
        raise exception 'FLB#143 validation failed: a required migrated identity is null.';
    end if;

    if (select count(*) from "Catch" where "TripId" is not null)
        <> (select count(*) from catches where tripid is not null)
        or (select count(*) from "TripParticipant") <> (select count(*) from tripparticipants)
        or (select count(*) from "TripNote") <> (select count(*) from tripnotes)
        or (select count(*) from "TripPhotograph") <> (select count(*) from tripphotographs)
        or (select count(*) from "CatchPhotograph") <> (select count(*) from catchphotographs)
    then
        raise exception 'FLB#143 validation failed: representative Trip/photograph relationship counts differ.';
    end if;
end $$;

commit;

-- Human-readable summary emitted only after all hard assertions and commit.
select 'users' as table_name, count(*) as copied_rows from users
union all select 'useridentities', count(*) from useridentities
union all select 'profiles', count(*) from profiles
union all select 'userplatformcapabilities', count(*) from userplatformcapabilities
union all select 'userfishingmethodpreferences', count(*) from userfishingmethodpreferences
union all select 'userfishingspeciespreferences', count(*) from userfishingspeciespreferences
union all select 'trips', count(*) from trips
union all select 'tripparticipants', count(*) from tripparticipants
union all select 'tripphotographs', count(*) from tripphotographs
union all select 'tripnotes', count(*) from tripnotes
union all select 'userfishinglocationpreferences', count(*) from userfishinglocationpreferences
union all select 'catches', count(*) from catches
union all select 'catchphotographs', count(*) from catchphotographs
order by table_name;
