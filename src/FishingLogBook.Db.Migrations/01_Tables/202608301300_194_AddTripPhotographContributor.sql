ALTER TABLE "TripPhotograph"
    ADD COLUMN IF NOT EXISTS "ContributedByUserId" uuid NULL;

ALTER TABLE "TripPhotograph"
    DROP CONSTRAINT IF EXISTS "FkTripPhotographContributedByUser";

ALTER TABLE "TripPhotograph"
    ADD CONSTRAINT "FkTripPhotographContributedByUser"
        FOREIGN KEY ("ContributedByUserId") REFERENCES "User" ("Id");

CREATE INDEX IF NOT EXISTS "IxTripPhotographContributedByUserId"
    ON "TripPhotograph" ("ContributedByUserId");
