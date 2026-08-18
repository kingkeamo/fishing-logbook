ALTER TABLE "Catch"
    ADD COLUMN IF NOT EXISTS "SpeciesName" text NULL,
    ADD COLUMN IF NOT EXISTS "Weight" numeric(8, 3) NULL,
    ADD COLUMN IF NOT EXISTS "Length" numeric(8, 2) NULL,
    ADD COLUMN IF NOT EXISTS "Method" text NULL,
    ADD COLUMN IF NOT EXISTS "BaitOrLure" text NULL,
    ADD COLUMN IF NOT EXISTS "Notes" text NULL;

ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "Catch_Weight_Range";

ALTER TABLE "Catch"
    ADD CONSTRAINT "Catch_Weight_Range" CHECK (
        "Weight" IS NULL
        OR ("Weight" > 0 AND "Weight" <= 1000)
    );

ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "Catch_Length_Range";

ALTER TABLE "Catch"
    ADD CONSTRAINT "Catch_Length_Range" CHECK (
        "Length" IS NULL
        OR ("Length" > 0 AND "Length" <= 1000)
    );
