CREATE TABLE IF NOT EXISTS "TripParticipant"
(
    "Id"              uuid        NOT NULL,
    "TripId"          uuid        NOT NULL,
    "UserId"          uuid        NOT NULL,
    "Status"          text        NOT NULL,
    "InvitedByUserId" uuid        NOT NULL,
    "InvitedOn"       timestamptz NOT NULL,
    "RespondedOn"     timestamptz NULL,
    "RemovedOn"       timestamptz NULL,
    "CreatedOn"       timestamptz NOT NULL DEFAULT now(),
    "UpdatedOn"       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkTripParticipant" PRIMARY KEY ("Id"),
    CONSTRAINT "FkTripParticipantTrip" FOREIGN KEY ("TripId") REFERENCES "Trip" ("Id"),
    CONSTRAINT "FkTripParticipantUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "FkTripParticipantInvitedByUser" FOREIGN KEY ("InvitedByUserId") REFERENCES "User" ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UxTripParticipantTripUser"
    ON "TripParticipant" ("TripId", "UserId");

CREATE INDEX IF NOT EXISTS "IxTripParticipantUserStatus"
    ON "TripParticipant" ("UserId", "Status");

ALTER TABLE "TripParticipant"
    DROP CONSTRAINT IF EXISTS "TripParticipant_Status_Allowed";

ALTER TABLE "TripParticipant"
    ADD CONSTRAINT "TripParticipant_Status_Allowed" CHECK (
        "Status" IN ('Pending', 'Accepted', 'Declined')
    );

ALTER TABLE "TripParticipant"
    DROP CONSTRAINT IF EXISTS "TripParticipant_Responded_After_Invited";

ALTER TABLE "TripParticipant"
    ADD CONSTRAINT "TripParticipant_Responded_After_Invited" CHECK (
        "RespondedOn" IS NULL OR "RespondedOn" >= "InvitedOn"
    );

ALTER TABLE "TripParticipant"
    DROP CONSTRAINT IF EXISTS "TripParticipant_Pending_Has_No_Response";

ALTER TABLE "TripParticipant"
    ADD CONSTRAINT "TripParticipant_Pending_Has_No_Response" CHECK (
        "Status" <> 'Pending' OR "RespondedOn" IS NULL
    );

ALTER TABLE "TripParticipant"
    DROP CONSTRAINT IF EXISTS "TripParticipant_Not_Self_Invited";

ALTER TABLE "TripParticipant"
    ADD CONSTRAINT "TripParticipant_Not_Self_Invited" CHECK (
        "UserId" <> "InvitedByUserId"
    );

ALTER TABLE "TripParticipant"
    DROP CONSTRAINT IF EXISTS "TripParticipant_Removed_Was_Accepted";

ALTER TABLE "TripParticipant"
    ADD CONSTRAINT "TripParticipant_Removed_Was_Accepted" CHECK (
        "RemovedOn" IS NULL OR "Status" = 'Accepted'
    );
