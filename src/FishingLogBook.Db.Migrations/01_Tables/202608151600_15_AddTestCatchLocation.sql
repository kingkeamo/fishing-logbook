ALTER TABLE "TestCatch"
    ADD COLUMN IF NOT EXISTS "Latitude" double precision NULL,
    ADD COLUMN IF NOT EXISTS "Longitude" double precision NULL,
    ADD COLUMN IF NOT EXISTS "LocationAccuracyMetres" double precision NULL,
    ADD COLUMN IF NOT EXISTS "LocationCapturedOn" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "LocationSource" text NULL,
    ADD COLUMN IF NOT EXISTS "LocationVisibility" text NULL,
    ADD COLUMN IF NOT EXISTS "LocationConsentVersion" text NULL;
