CREATE TABLE schema_info (
    version INTEGER NOT NULL
);

INSERT INTO schema_info (version) VALUES (0);

CREATE TABLE local_trips (
    id TEXT NOT NULL PRIMARY KEY,
    user_id TEXT NOT NULL,
    title TEXT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX ix_local_trips_user_id ON local_trips (user_id);

CREATE TABLE local_catches (
    id TEXT NOT NULL PRIMARY KEY,
    user_id TEXT NOT NULL,
    trip_id TEXT NULL,
    caught_by_user_id TEXT NOT NULL,
    recorded_by_user_id TEXT NOT NULL,
    caught_on TEXT NOT NULL,
    species_name TEXT NULL,
    weight_grams REAL NULL,
    length_millimetres REAL NULL,
    method TEXT NULL,
    bait_or_lure TEXT NULL,
    notes TEXT NULL,
    place_name TEXT NULL,
    latitude REAL NULL,
    longitude REAL NULL,
    location_accuracy_metres REAL NULL,
    location_captured_on TEXT NULL,
    location_source TEXT NULL,
    location_visibility TEXT NULL,
    location_consent_version TEXT NULL,
    revision INTEGER NOT NULL,
    lifecycle TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (trip_id) REFERENCES local_trips (id)
);

CREATE INDEX ix_local_catches_user_id ON local_catches (user_id);

CREATE TABLE local_catch_photographs (
    id TEXT NOT NULL PRIMARY KEY,
    catch_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    local_file_path TEXT NULL,
    content_type TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (catch_id) REFERENCES local_catches (id) ON DELETE CASCADE
);

CREATE INDEX ix_local_catch_photographs_user_id ON local_catch_photographs (user_id);
CREATE INDEX ix_local_catch_photographs_catch_id ON local_catch_photographs (catch_id);

CREATE TABLE outbox_operations (
    id TEXT NOT NULL PRIMARY KEY,
    user_id TEXT NOT NULL,
    aggregate_type TEXT NOT NULL,
    aggregate_id TEXT NOT NULL,
    operation_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    status TEXT NOT NULL,
    attempt_count INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    available_at TEXT NOT NULL,
    last_attempt_at TEXT NULL,
    last_error TEXT NULL
);

CREATE INDEX ix_outbox_operations_user_id ON outbox_operations (user_id);
CREATE INDEX ix_outbox_operations_status ON outbox_operations (status);

CREATE UNIQUE INDEX ux_outbox_pending_coalesce
    ON outbox_operations (user_id, aggregate_type, aggregate_id, operation_type)
    WHERE status = 'Pending';
