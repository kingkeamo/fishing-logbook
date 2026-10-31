ALTER TABLE outboxoperations ADD COLUMN leaseowner TEXT NULL;
ALTER TABLE outboxoperations ADD COLUMN leaseexpiresat TEXT NULL;

CREATE INDEX ixoutboxoperationsclaim ON outboxoperations (userid, status, availableat);

CREATE TABLE localcatchsyncs (
    catchid TEXT NOT NULL PRIMARY KEY,
    userid TEXT NOT NULL,
    syncedrevision INTEGER NOT NULL,
    syncedon TEXT NOT NULL,
    FOREIGN KEY (catchid) REFERENCES localcatches (id) ON DELETE CASCADE
);

CREATE INDEX ixlocalcatchsyncsuserid ON localcatchsyncs (userid);

CREATE TABLE localphotographsyncs (
    photographid TEXT NOT NULL PRIMARY KEY,
    catchid TEXT NOT NULL,
    userid TEXT NOT NULL,
    status TEXT NOT NULL,
    objectkey TEXT NULL,
    updatedon TEXT NOT NULL
);

CREATE INDEX ixlocalphotographsyncscatch ON localphotographsyncs (catchid, userid);
