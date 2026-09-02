-- FLB#143: remove the superseded quoted PascalCase tables after the lowercase
-- schema has been populated, validated, and exercised by the application.
--
-- Destructive operation prerequisites:
--   1. Stop all application writes and enter a maintenance window.
--   2. Take and verify a database backup.
--   3. Successfully run 02-copy-data-to-new-schema.sql.
--   4. Verify the application against the lowercase schema.
--   5. Run this entire file as one script. Do not run selected statements.
--
-- The script revalidates every copied row immediately before cleanup. It keeps
-- the lowercase schema and its canonical systemhealth seed. The historical
-- quoted SystemTest seed is removed with the rest of the old tables.
--
-- DROP TABLE intentionally does not use CASCADE. Any unexpected dependency
-- aborts the transaction rather than removing an unaudited object.

begin;

-- Refuse to start if the expected old or new schema is incomplete.
do $$
declare
    required_table text;
begin
    foreach required_table in array array[
        '"SystemTest"', '"User"', '"UserIdentity"', '"Profile"',
        '"PlatformCapability"', '"UserPlatformCapability"',
        '"FishingMethod"', '"Species"', '"UserFishingMethodPreference"',
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
            raise exception 'FLB#143 cleanup blocked: required table % does not exist.', required_table;
        end if;
    end loop;
end $$;

-- Re-prove that every old source row exists unchanged in the destination.
-- Additional lowercase rows created after cutover are intentionally allowed.
do $$
begin
    if exists (
            select "Id", "Email", "CreatedOn", "OfflineAccessEnabled", "OfflineAccessEnabledAt" from "User"
            except
            select id, email, createdon, offlineaccessenabled, offlineaccessenabledat from users
        )
    then raise exception 'FLB#143 cleanup blocked: users differ from old User.'; end if;

    if exists (
            select "Id", "UserId", "Provider", "Subject", "CreatedOn" from "UserIdentity"
            except
            select id, userid, provider, subject, createdon from useridentities
        )
    then raise exception 'FLB#143 cleanup blocked: useridentities differ from old UserIdentity.'; end if;

    if exists (
            select "UserId", "DisplayName", "PhotographId", "PhotographObjectKey",
                "PhotographContentType", "HomeRegion", "PreferredWeightUnit",
                "PreferredLengthUnit", "ShowDisplayName", "ShowPhotograph", "ShowHomeRegion",
                "ShowPreferredFishingMethods", "ShowPreferredSpecies",
                "OnboardingCompletedOn", "CreatedOn", "UpdatedOn"
            from "Profile"
            except
            select userid, displayname, photographid, photographobjectkey,
                photographcontenttype, homeregion, preferredweightunit,
                preferredlengthunit, showdisplayname, showphotograph, showhomeregion,
                showpreferredfishingmethods, showpreferredspecies,
                onboardingcompletedon, createdon, updatedon
            from profiles
        )
    then raise exception 'FLB#143 cleanup blocked: profiles differ from old Profile.'; end if;

    if exists (
            select "Code", "CreatedOn" from "PlatformCapability"
            except select code, createdon from platformcapabilities
        )
    then raise exception 'FLB#143 cleanup blocked: platformcapabilities differ from old PlatformCapability.'; end if;

    if exists (
            select "UserId", "CapabilityCode", "CreatedOn" from "UserPlatformCapability"
            except select userid, capabilitycode, createdon from userplatformcapabilities
        )
    then raise exception 'FLB#143 cleanup blocked: userplatformcapabilities differ from old UserPlatformCapability.'; end if;

    if exists (
            select "Id", "Code", "Name", "CreatedOn" from "FishingMethod"
            except select id, code, name, createdon from fishingmethods
        )
    then raise exception 'FLB#143 cleanup blocked: fishingmethods differ from old FishingMethod.'; end if;

    if exists (
            select "Id", "Code", "Name", "CreatedOn" from "Species"
            except select id, code, name, createdon from species
        )
    then raise exception 'FLB#143 cleanup blocked: species differ from old Species.'; end if;

    if exists (
            select "UserId", "FishingMethodId", "IsDefault", "CreatedOn" from "UserFishingMethodPreference"
            except select userid, fishingmethodid, isdefault, createdon from userfishingmethodpreferences
        )
    then raise exception 'FLB#143 cleanup blocked: userfishingmethodpreferences differ from old UserFishingMethodPreference.'; end if;

    if exists (
            select "UserId", "FishingMethodId", "SpeciesId", "IsDefault", "CreatedOn"
            from "UserFishingSpeciesPreference"
            except
            select userid, fishingmethodid, speciesid, isdefault, createdon
            from userfishingspeciespreferences
        )
    then raise exception 'FLB#143 cleanup blocked: userfishingspeciespreferences differ from old UserFishingSpeciesPreference.'; end if;

    if exists (
            select "Id", "OwnerUserId", "Title", "PlaceName", "Status", "StartedOn", "EndedOn",
                "Latitude", "Longitude", "LocationAccuracyMetres", "LocationCapturedOn",
                "LocationSource", "LocationVisibility", "LocationConsentVersion",
                "CreatedOn", "UpdatedOn"
            from "Trip"
            except
            select id, owneruserid, title, placename, status, startedon, endedon,
                latitude, longitude, locationaccuracymetres, locationcapturedon,
                locationsource, locationvisibility, locationconsentversion,
                createdon, updatedon
            from trips
        )
    then raise exception 'FLB#143 cleanup blocked: trips differ from old Trip.'; end if;

    if exists (
            select "Id", "TripId", "UserId", "Status", "InvitedByUserId", "InvitedOn",
                "RespondedOn", "RemovedOn", "CreatedOn", "UpdatedOn"
            from "TripParticipant"
            except
            select id, tripid, userid, status, invitedbyuserid, invitedon,
                respondedon, removedon, createdon, updatedon
            from tripparticipants
        )
    then raise exception 'FLB#143 cleanup blocked: tripparticipants differ from old TripParticipant.'; end if;

    if exists (
            select "Id", "TripId", "ObjectKey", "ContentType", "CapturedOn", "AddedOn",
                "ContributedByUserId", "CreatedOn", "UpdatedOn"
            from "TripPhotograph"
            except
            select id, tripid, objectkey, contenttype, capturedon, addedon,
                contributedbyuserid, createdon, updatedon
            from tripphotographs
        )
    then raise exception 'FLB#143 cleanup blocked: tripphotographs differ from old TripPhotograph.'; end if;

    if exists (
            select "Id", "TripId", "CreatedByUserId", "Text", "RecordedOn", "CreatedOn", "UpdatedOn"
            from "TripNote"
            except
            select id, tripid, createdbyuserid, text, recordedon, createdon, updatedon
            from tripnotes
        )
    then raise exception 'FLB#143 cleanup blocked: tripnotes differ from old TripNote.'; end if;

    if exists (
            select "Id", "UserId", "Name", "IsDefault", "CreatedOn"
            from "UserFishingLocationPreference"
            except
            select id, userid, name, isdefault, createdon
            from userfishinglocationpreferences
        )
    then raise exception 'FLB#143 cleanup blocked: userfishinglocationpreferences differ from old UserFishingLocationPreference.'; end if;

    if exists (
            select "Id",
                coalesce("CaughtByUserId", "AnglerUserId", "UserId"),
                coalesce("RecordedByUserId", "UserId"),
                "CaughtOn", "CreatedOn", "Latitude", "Longitude",
                "LocationAccuracyMetres", "LocationCapturedOn", "LocationSource",
                "LocationVisibility", "LocationConsentVersion", "SpeciesName", "Weight",
                "Length", "Method", "BaitOrLure", "Notes", "TripId"
            from "Catch"
            except
            select id, caughtbyuserid, recordedbyuserid, caughton, createdon,
                latitude, longitude, locationaccuracymetres, locationcapturedon,
                locationsource, locationvisibility, locationconsentversion, speciesname,
                weight, length, method, baitorlure, notes, tripid
            from catches
        )
    then raise exception 'FLB#143 cleanup blocked: catches differ from canonical old Catch values.'; end if;

    if exists (
            select "Id", "CatchId", "ContentType" from "CatchPhotograph"
            except select id, catchid, contenttype from catchphotographs
        )
    then raise exception 'FLB#143 cleanup blocked: catchphotographs differ from old CatchPhotograph.'; end if;

    if (select count(*) from systemhealth) <> 1 then
        raise exception 'FLB#143 cleanup blocked: lowercase systemhealth must contain exactly one canonical row.';
    end if;
end $$;

-- Drop children before parents. Owned constraints and indexes are removed with
-- their tables; no independent legacy routines, views, triggers, or types exist.
drop table "CatchPhotograph";
drop table "Catch";
drop table "TripNote";
drop table "TripPhotograph";
drop table "TripParticipant";
drop table "Trip";
drop table "UserFishingLocationPreference";
drop table "UserFishingSpeciesPreference";
drop table "UserFishingMethodPreference";
drop table "UserPlatformCapability";
drop table "Species";
drop table "FishingMethod";
drop table "PlatformCapability";
drop table "Profile";
drop table "UserIdentity";
drop table "User";
drop table "SystemTest";

commit;

select 'FLB#143 cleanup complete: superseded quoted tables removed; lowercase schema retained.' as result;
