CREATE TABLE localtripdocuments (
    tripid TEXT NOT NULL,
    userid TEXT NOT NULL,
    payloadjson TEXT NOT NULL,
    revision INTEGER NOT NULL DEFAULT 0,
    syncedrevision INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (tripid, userid)
);

CREATE TABLE localtripcontributions (
    id TEXT NOT NULL,
    userid TEXT NOT NULL,
    tripid TEXT NOT NULL,
    payloadjson TEXT NOT NULL,
    localfilepath TEXT NULL,
    revision INTEGER NOT NULL DEFAULT 0,
    syncedrevision INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (id, userid),
    FOREIGN KEY (tripid, userid) REFERENCES localtripdocuments (tripid, userid)
);

CREATE INDEX ixlocaltripcontributionsusertrip ON localtripcontributions (userid, tripid);
