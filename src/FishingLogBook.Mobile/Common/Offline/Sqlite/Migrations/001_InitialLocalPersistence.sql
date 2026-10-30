CREATE TABLE schemainfo (
    version INTEGER NOT NULL
);

INSERT INTO schemainfo (version) VALUES (0);

CREATE TABLE localtrips (
    id TEXT NOT NULL PRIMARY KEY,
    userid TEXT NOT NULL,
    title TEXT NULL,
    updatedon TEXT NOT NULL
);

CREATE INDEX ixlocaltripsuserid ON localtrips (userid);

CREATE TABLE localcatches (
    id TEXT NOT NULL PRIMARY KEY,
    userid TEXT NOT NULL,
    tripid TEXT NULL,
    caughtbyuserid TEXT NOT NULL,
    recordedbyuserid TEXT NOT NULL,
    caughton TEXT NOT NULL,
    speciesname TEXT NULL,
    weight REAL NULL,
    length REAL NULL,
    method TEXT NULL,
    baitorlure TEXT NULL,
    notes TEXT NULL,
    placename TEXT NULL,
    latitude REAL NULL,
    longitude REAL NULL,
    locationaccuracymetres REAL NULL,
    locationcapturedon TEXT NULL,
    locationsource TEXT NULL,
    locationvisibility TEXT NULL,
    locationconsentversion TEXT NULL,
    revision INTEGER NOT NULL,
    lifecycle TEXT NOT NULL,
    createdon TEXT NOT NULL,
    updatedon TEXT NOT NULL,
    FOREIGN KEY (tripid) REFERENCES localtrips (id)
);

CREATE INDEX ixlocalcatchesuserid ON localcatches (userid);

CREATE TABLE localcatchphotographs (
    id TEXT NOT NULL PRIMARY KEY,
    catchid TEXT NOT NULL,
    userid TEXT NOT NULL,
    sortorder INTEGER NOT NULL,
    localfilepath TEXT NULL,
    contenttype TEXT NULL,
    createdon TEXT NOT NULL,
    FOREIGN KEY (catchid) REFERENCES localcatches (id) ON DELETE CASCADE
);

CREATE INDEX ixlocalcatchphotographsuserid ON localcatchphotographs (userid);
CREATE INDEX ixlocalcatchphotographscatchid ON localcatchphotographs (catchid);

CREATE TABLE outboxoperations (
    id TEXT NOT NULL PRIMARY KEY,
    userid TEXT NOT NULL,
    aggregatetype TEXT NOT NULL,
    aggregateid TEXT NOT NULL,
    operationtype TEXT NOT NULL,
    payloadjson TEXT NOT NULL,
    status TEXT NOT NULL,
    attemptcount INTEGER NOT NULL,
    createdon TEXT NOT NULL,
    availableat TEXT NOT NULL,
    lastattemptat TEXT NULL,
    lasterror TEXT NULL
);

CREATE INDEX ixoutboxoperationsuserid ON outboxoperations (userid);
CREATE INDEX ixoutboxoperationsstatus ON outboxoperations (status);

CREATE UNIQUE INDEX uxoutboxpendingcoalesce
    ON outboxoperations (userid, aggregatetype, aggregateid, operationtype)
    WHERE status = 'Pending';
