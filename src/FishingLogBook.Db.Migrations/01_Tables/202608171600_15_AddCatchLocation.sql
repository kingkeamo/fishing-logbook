ALTER TABLE "Catch"
    ADD COLUMN IF NOT EXISTS "Latitude" double precision NULL,
    ADD COLUMN IF NOT EXISTS "Longitude" double precision NULL,
    ADD COLUMN IF NOT EXISTS "LocationAccuracyMetres" double precision NULL,
    ADD COLUMN IF NOT EXISTS "LocationCapturedOn" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "LocationSource" text NULL,
    ADD COLUMN IF NOT EXISTS "LocationVisibility" text NULL,
    ADD COLUMN IF NOT EXISTS "LocationConsentVersion" text NULL;

ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "Catch_Location_Coherent";

ALTER TABLE "Catch"
    ADD CONSTRAINT "Catch_Location_Coherent" CHECK (
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
