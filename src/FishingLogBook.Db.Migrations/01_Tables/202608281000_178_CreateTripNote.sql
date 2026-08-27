CREATE TABLE IF NOT EXISTS "TripNote"
(
    "Id"                uuid        NOT NULL,
    "TripId"            uuid        NOT NULL,
    "CreatedByUserId"   uuid        NOT NULL,
    "Text"              text        NOT NULL,
    "RecordedOn"        timestamptz NOT NULL,
    "CreatedOn"         timestamptz NOT NULL DEFAULT now(),
    "UpdatedOn"         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkTripNote" PRIMARY KEY ("Id"),
    CONSTRAINT "FkTripNoteTrip" FOREIGN KEY ("TripId") REFERENCES "Trip" ("Id"),
    CONSTRAINT "FkTripNoteCreatedByUser" FOREIGN KEY ("CreatedByUserId") REFERENCES "User" ("Id")
);

CREATE INDEX IF NOT EXISTS "IxTripNoteTripId" ON "TripNote" ("TripId");

CREATE INDEX IF NOT EXISTS "IxTripNoteTripRecordedOn" ON "TripNote" ("TripId", "RecordedOn");
