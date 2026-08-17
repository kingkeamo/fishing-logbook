CREATE TABLE IF NOT EXISTS "Catch"
(
    "Id"        uuid        NOT NULL,
    "UserId"    uuid        NOT NULL,
    "CaughtOn"  timestamptz NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkCatch" PRIMARY KEY ("Id"),
    CONSTRAINT "FkCatchUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id")
);

CREATE INDEX IF NOT EXISTS "IxCatchUserId" ON "Catch" ("UserId");

CREATE TABLE IF NOT EXISTS "CatchPhotograph"
(
    "Id"          uuid NOT NULL,
    "CatchId"     uuid NOT NULL,
    "ContentType" text NOT NULL,
    CONSTRAINT "PkCatchPhotograph" PRIMARY KEY ("Id"),
    CONSTRAINT "FkCatchPhotographCatch" FOREIGN KEY ("CatchId") REFERENCES "Catch" ("Id")
);

CREATE INDEX IF NOT EXISTS "IxCatchPhotographCatchId" ON "CatchPhotograph" ("CatchId");
