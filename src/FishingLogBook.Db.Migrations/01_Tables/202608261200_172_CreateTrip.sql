CREATE TABLE IF NOT EXISTS "Trip"
(
    "Id"                     uuid             NOT NULL,
    "OwnerUserId"            uuid             NOT NULL,
    "Title"                  text             NULL,
    "PlaceName"              text             NULL,
    "Status"                 text             NOT NULL,
    "StartedOn"              timestamptz      NOT NULL,
    "EndedOn"                timestamptz      NULL,
    "Latitude"               double precision NULL,
    "Longitude"              double precision NULL,
    "LocationAccuracyMetres" double precision NULL,
    "LocationCapturedOn"     timestamptz      NULL,
    "LocationSource"         text             NULL,
    "LocationVisibility"     text             NULL,
    "LocationConsentVersion" text             NULL,
    "CreatedOn"              timestamptz      NOT NULL DEFAULT now(),
    "UpdatedOn"              timestamptz      NOT NULL DEFAULT now(),
    CONSTRAINT "PkTrip" PRIMARY KEY ("Id"),
    CONSTRAINT "FkTripOwnerUser" FOREIGN KEY ("OwnerUserId") REFERENCES "User" ("Id")
);

CREATE INDEX IF NOT EXISTS "IxTripOwnerUserId" ON "Trip" ("OwnerUserId");

CREATE UNIQUE INDEX IF NOT EXISTS "UxTripOwnerActive"
    ON "Trip" ("OwnerUserId")
    WHERE "Status" = 'Active';

ALTER TABLE "Trip"
    DROP CONSTRAINT IF EXISTS "Trip_Status_Allowed";

ALTER TABLE "Trip"
    ADD CONSTRAINT "Trip_Status_Allowed" CHECK (
        "Status" IN ('Active', 'Completed')
    );

ALTER TABLE "Trip"
    DROP CONSTRAINT IF EXISTS "Trip_Ended_After_Started";

ALTER TABLE "Trip"
    ADD CONSTRAINT "Trip_Ended_After_Started" CHECK (
        "EndedOn" IS NULL OR "EndedOn" >= "StartedOn"
    );

ALTER TABLE "Trip"
    DROP CONSTRAINT IF EXISTS "Trip_Active_Has_No_End";

ALTER TABLE "Trip"
    ADD CONSTRAINT "Trip_Active_Has_No_End" CHECK (
        "Status" <> 'Active' OR "EndedOn" IS NULL
    );

ALTER TABLE "Trip"
    DROP CONSTRAINT IF EXISTS "Trip_Location_Coherent";

ALTER TABLE "Trip"
    ADD CONSTRAINT "Trip_Location_Coherent" CHECK (
        (
            "Latitude" IS NULL
            AND "Longitude" IS NULL
            AND "LocationAccuracyMetres" IS NULL
            AND "LocationCapturedOn" IS NULL
            AND "LocationSource" IS NULL
            AND "LocationVisibility" IS NULL
            AND "LocationConsentVersion" IS NULL
        )
        OR
        (
            "Latitude" IS NOT NULL
            AND "Longitude" IS NOT NULL
            AND "Latitude" BETWEEN -90 AND 90
            AND "Longitude" BETWEEN -180 AND 180
            AND "LocationCapturedOn" IS NOT NULL
            AND "LocationSource" IS NOT NULL
            AND "LocationVisibility" IS NOT NULL
            AND "LocationConsentVersion" IS NOT NULL
        )
    );

ALTER TABLE "Trip"
    DROP CONSTRAINT IF EXISTS "Trip_LocationVisibility_Allowed";

ALTER TABLE "Trip"
    ADD CONSTRAINT "Trip_LocationVisibility_Allowed" CHECK (
        "LocationVisibility" IS NULL
        OR "LocationVisibility" IN ('Private', 'Approximate', 'FishingVenueOnly', 'Public')
    );
