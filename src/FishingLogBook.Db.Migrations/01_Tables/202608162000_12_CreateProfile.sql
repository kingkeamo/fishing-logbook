CREATE TABLE IF NOT EXISTS "Profile"
(
    "UserId"                     uuid        NOT NULL,
    "DisplayName"                text        NULL,
    "PhotographId"               uuid        NULL,
    "PhotographObjectKey"        text        NULL,
    "PhotographContentType"      text        NULL,
    "HomeRegion"                 text        NULL,
    "PreferredFishingTypes"      text[]      NOT NULL DEFAULT '{}',
    "PreferredSpecies"           text[]      NOT NULL DEFAULT '{}',
    "ShowDisplayName"            boolean     NOT NULL DEFAULT true,
    "ShowPhotograph"             boolean     NOT NULL DEFAULT false,
    "ShowHomeRegion"             boolean     NOT NULL DEFAULT false,
    "ShowPreferredFishingTypes"  boolean     NOT NULL DEFAULT false,
    "ShowPreferredSpecies"       boolean     NOT NULL DEFAULT false,
    "CreatedOn"                  timestamptz NOT NULL DEFAULT now(),
    "UpdatedOn"                  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkProfile" PRIMARY KEY ("UserId"),
    CONSTRAINT "FkProfileUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id")
);
