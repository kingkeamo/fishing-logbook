ALTER TABLE "Profile"
    ADD COLUMN IF NOT EXISTS "PreferredWeightUnit" integer NULL,
    ADD COLUMN IF NOT EXISTS "PreferredLengthUnit" integer NULL;

UPDATE "Profile"
SET "PreferredWeightUnit" = 0
WHERE "PreferredWeightUnit" IS NULL;

UPDATE "Profile"
SET "PreferredLengthUnit" = 0
WHERE "PreferredLengthUnit" IS NULL;

ALTER TABLE "Profile"
    ALTER COLUMN "PreferredWeightUnit" SET DEFAULT 0,
    ALTER COLUMN "PreferredLengthUnit" SET DEFAULT 0,
    ALTER COLUMN "PreferredWeightUnit" SET NOT NULL,
    ALTER COLUMN "PreferredLengthUnit" SET NOT NULL;

ALTER TABLE "Profile"
    DROP CONSTRAINT IF EXISTS "Profile_PreferredWeightUnit_Check";

ALTER TABLE "Profile"
    ADD CONSTRAINT "Profile_PreferredWeightUnit_Check"
        CHECK ("PreferredWeightUnit" IN (0, 1));

ALTER TABLE "Profile"
    DROP CONSTRAINT IF EXISTS "Profile_PreferredLengthUnit_Check";

ALTER TABLE "Profile"
    ADD CONSTRAINT "Profile_PreferredLengthUnit_Check"
        CHECK ("PreferredLengthUnit" IN (0, 1));
