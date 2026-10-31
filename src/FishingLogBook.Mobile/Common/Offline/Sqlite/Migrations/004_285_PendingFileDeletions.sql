CREATE TABLE localpendingfiledeletions (
    id TEXT NOT NULL PRIMARY KEY,
    userid TEXT NOT NULL,
    relativepath TEXT NOT NULL,
    createdon TEXT NOT NULL
);

CREATE INDEX ixlocalpendingfiledeletionsuserid ON localpendingfiledeletions (userid);
