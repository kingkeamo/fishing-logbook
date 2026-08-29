UPDATE "TripPhotograph" p
SET "ContributedByUserId" = t."OwnerUserId"
FROM "Trip" t
WHERE t."Id" = p."TripId"
  AND p."ContributedByUserId" IS NULL;

ALTER TABLE "TripPhotograph"
    ALTER COLUMN "ContributedByUserId" SET NOT NULL;
