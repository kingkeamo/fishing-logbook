ALTER TABLE "Catch"
    ADD COLUMN IF NOT EXISTS "AnglerUserId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "RecordedByUserId" uuid NULL;

ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "FkCatchAnglerUser";

ALTER TABLE "Catch"
    ADD CONSTRAINT "FkCatchAnglerUser" FOREIGN KEY ("AnglerUserId") REFERENCES "User" ("Id");

ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "FkCatchRecordedByUser";

ALTER TABLE "Catch"
    ADD CONSTRAINT "FkCatchRecordedByUser" FOREIGN KEY ("RecordedByUserId") REFERENCES "User" ("Id");

UPDATE "Catch"
SET "AnglerUserId" = "UserId"
WHERE "AnglerUserId" IS NULL;

UPDATE "Catch"
SET "RecordedByUserId" = "UserId"
WHERE "RecordedByUserId" IS NULL;
