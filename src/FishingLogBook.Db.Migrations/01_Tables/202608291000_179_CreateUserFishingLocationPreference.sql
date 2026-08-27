CREATE TABLE IF NOT EXISTS "UserFishingLocationPreference"
(
    "Id"        uuid        NOT NULL,
    "UserId"    uuid        NOT NULL,
    "Name"      text        NOT NULL,
    "IsDefault" boolean     NOT NULL DEFAULT false,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkUserFishingLocationPreference" PRIMARY KEY ("Id"),
    CONSTRAINT "FkUserFishingLocationPreferenceUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "CkUserFishingLocationPreferenceName" CHECK (btrim("Name") <> '' AND length("Name") <= 160)
);

CREATE INDEX IF NOT EXISTS "IxUserFishingLocationPreferenceUserId"
    ON "UserFishingLocationPreference" ("UserId");

CREATE UNIQUE INDEX IF NOT EXISTS "UxUserFishingLocationPreferenceName"
    ON "UserFishingLocationPreference" ("UserId", lower(btrim("Name")));

CREATE UNIQUE INDEX IF NOT EXISTS "UxUserFishingLocationPreferenceDefault"
    ON "UserFishingLocationPreference" ("UserId")
    WHERE "IsDefault" = true;
