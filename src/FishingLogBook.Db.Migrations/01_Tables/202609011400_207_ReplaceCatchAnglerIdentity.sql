DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "Catch"
        WHERE "AnglerUserId" IS NOT NULL
          AND "UserId" IS NOT NULL
          AND "AnglerUserId" <> "UserId")
    THEN
        RAISE EXCEPTION 'Catch rows exist where UserId and AnglerUserId differ.';
    END IF;
END $$;

ALTER TABLE "Catch"
    ADD COLUMN IF NOT EXISTS "CaughtByUserId" uuid NULL;

UPDATE "Catch"
SET "CaughtByUserId" = COALESCE("AnglerUserId", "UserId")
WHERE "CaughtByUserId" IS NULL;

ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "FkCatchCaughtByUser";

ALTER TABLE "Catch"
    ADD CONSTRAINT "FkCatchCaughtByUser"
        FOREIGN KEY ("CaughtByUserId") REFERENCES "User" ("Id");

CREATE INDEX IF NOT EXISTS "IxCatchCaughtByUserId" ON "Catch" ("CaughtByUserId");
