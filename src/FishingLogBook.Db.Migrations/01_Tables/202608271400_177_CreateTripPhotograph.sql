CREATE TABLE IF NOT EXISTS "TripPhotograph"
(
    "Id"          uuid        NOT NULL,
    "TripId"      uuid        NOT NULL,
    "ObjectKey"   text        NOT NULL,
    "ContentType" text        NOT NULL,
    "CapturedOn"  timestamptz NULL,
    "AddedOn"     timestamptz NOT NULL,
    "CreatedOn"   timestamptz NOT NULL DEFAULT now(),
    "UpdatedOn"   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkTripPhotograph" PRIMARY KEY ("Id"),
    CONSTRAINT "FkTripPhotographTrip" FOREIGN KEY ("TripId") REFERENCES "Trip" ("Id")
);

CREATE INDEX IF NOT EXISTS "IxTripPhotographTripId" ON "TripPhotograph" ("TripId");

CREATE UNIQUE INDEX IF NOT EXISTS "UxTripPhotographObjectKey" ON "TripPhotograph" ("ObjectKey");
