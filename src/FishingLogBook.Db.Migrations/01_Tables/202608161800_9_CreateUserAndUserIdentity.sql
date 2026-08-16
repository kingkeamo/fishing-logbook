CREATE TABLE IF NOT EXISTS "User"
(
    "Id"        uuid        NOT NULL,
    "Email"     text        NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkUser" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "UserIdentity"
(
    "Id"        uuid        NOT NULL,
    "UserId"    uuid        NOT NULL,
    "Provider"  text        NOT NULL,
    "Subject"   text        NOT NULL,
    "CreatedOn" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "PkUserIdentity" PRIMARY KEY ("Id"),
    CONSTRAINT "FkUserIdentityUser" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "UxUserIdentityProviderSubject" UNIQUE ("Provider", "Subject")
);

CREATE INDEX IF NOT EXISTS "IxUserIdentityUserId" ON "UserIdentity" ("UserId");
