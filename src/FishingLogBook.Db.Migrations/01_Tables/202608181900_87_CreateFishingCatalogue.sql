CREATE TABLE IF NOT EXISTS "FishingMethod"
(
    "Id"        uuid        NOT NULL,
    "Code"      text        NOT NULL,
    "Name"      text        NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkFishingMethod" PRIMARY KEY ("Id"),
    CONSTRAINT "UxFishingMethodCode" UNIQUE ("Code")
);

CREATE TABLE IF NOT EXISTS "Species"
(
    "Id"        uuid        NOT NULL,
    "Code"      text        NOT NULL,
    "Name"      text        NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkSpecies" PRIMARY KEY ("Id"),
    CONSTRAINT "UxSpeciesCode" UNIQUE ("Code")
);

CREATE TABLE IF NOT EXISTS "UserFishingMethodPreference"
(
    "UserId"          uuid        NOT NULL,
    "FishingMethodId" uuid        NOT NULL,
    "IsDefault"       boolean     NOT NULL DEFAULT false,
    "CreatedOn"       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkUserFishingMethodPreference" PRIMARY KEY ("UserId", "FishingMethodId"),
    CONSTRAINT "FkUserFishingMethodPreferenceUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "FkUserFishingMethodPreferenceFishingMethod" FOREIGN KEY ("FishingMethodId") REFERENCES "FishingMethod" ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UxUserFishingMethodPreferenceDefault"
    ON "UserFishingMethodPreference" ("UserId")
    WHERE "IsDefault" = true;

CREATE TABLE IF NOT EXISTS "UserFishingSpeciesPreference"
(
    "UserId"          uuid        NOT NULL,
    "FishingMethodId" uuid        NOT NULL,
    "SpeciesId"       uuid        NOT NULL,
    "IsDefault"       boolean     NOT NULL DEFAULT false,
    "CreatedOn"       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkUserFishingSpeciesPreference" PRIMARY KEY ("UserId", "FishingMethodId", "SpeciesId"),
    CONSTRAINT "FkUserFishingSpeciesPreferenceUserMethod"
        FOREIGN KEY ("UserId", "FishingMethodId")
        REFERENCES "UserFishingMethodPreference" ("UserId", "FishingMethodId"),
    CONSTRAINT "FkUserFishingSpeciesPreferenceSpecies" FOREIGN KEY ("SpeciesId") REFERENCES "Species" ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UxUserFishingSpeciesPreferenceDefault"
    ON "UserFishingSpeciesPreference" ("UserId", "FishingMethodId")
    WHERE "IsDefault" = true;
