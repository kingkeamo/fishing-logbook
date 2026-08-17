INSERT INTO "PlatformCapability" ("Code")
SELECT v."Code"
FROM (VALUES
    ('Guide'),
    ('FishingVenueManager'),
    ('CompetitionOrganiser'),
    ('Administrator')
) AS v("Code")
WHERE NOT EXISTS (
    SELECT 1
    FROM "PlatformCapability" existing
    WHERE existing."Code" = v."Code");
