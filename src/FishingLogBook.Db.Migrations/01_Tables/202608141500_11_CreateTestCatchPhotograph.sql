CREATE TABLE IF NOT EXISTS "TestCatchPhotograph"
(
    "TestCatchId"  uuid NOT NULL,
    "PhotographId" uuid NOT NULL,
    "ObjectKey"    text NOT NULL,
    "ContentType"  text NOT NULL,
    CONSTRAINT "PkTestCatchPhotograph" PRIMARY KEY ("TestCatchId"),
    CONSTRAINT "FkTestCatchPhotographTestCatch" FOREIGN KEY ("TestCatchId") REFERENCES "TestCatch" ("Id")
);
