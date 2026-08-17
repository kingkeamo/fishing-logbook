CREATE TABLE IF NOT EXISTS "PlatformCapability"
(
    "Code"      text        NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkPlatformCapability" PRIMARY KEY ("Code")
);

CREATE TABLE IF NOT EXISTS "UserPlatformCapability"
(
    "UserId"         uuid        NOT NULL,
    "CapabilityCode" text        NOT NULL,
    "CreatedOn"      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkUserPlatformCapability" PRIMARY KEY ("UserId", "CapabilityCode"),
    CONSTRAINT "FkUserPlatformCapabilityUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "FkUserPlatformCapabilityCode" FOREIGN KEY ("CapabilityCode") REFERENCES "PlatformCapability" ("Code")
);
