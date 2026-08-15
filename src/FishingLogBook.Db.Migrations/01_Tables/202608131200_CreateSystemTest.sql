CREATE TABLE IF NOT EXISTS "SystemTest"
(
    "Id"        uuid        NOT NULL,
    "Name"      text        NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkSystemTest" PRIMARY KEY ("Id")
);
