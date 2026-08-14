CREATE TABLE IF NOT EXISTS "TestCatch"
(
    "Id"          uuid        NOT NULL,
    "SpeciesName" text        NOT NULL,
    "CaughtOn"    timestamptz NOT NULL,
    "Notes"       text        NULL,
    CONSTRAINT "PkTestCatch" PRIMARY KEY ("Id")
);
