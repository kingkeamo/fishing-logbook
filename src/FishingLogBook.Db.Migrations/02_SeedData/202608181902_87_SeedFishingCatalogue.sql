INSERT INTO "FishingMethod" ("Id", "Code", "Name")
SELECT v."Id"::uuid, v."Code", v."Name"
FROM (VALUES
    ('a7f1c2d0-0000-4000-8000-000000000001', 'Fly', 'Fly'),
    ('a7f1c2d0-0000-4000-8000-000000000002', 'Spinning', 'Spinning'),
    ('a7f1c2d0-0000-4000-8000-000000000003', 'Bait', 'Bait'),
    ('a7f1c2d0-0000-4000-8000-000000000004', 'Lure', 'Lure'),
    ('a7f1c2d0-0000-4000-8000-000000000005', 'Trolling', 'Trolling')
) AS v("Id", "Code", "Name")
WHERE NOT EXISTS (
    SELECT 1
    FROM "FishingMethod" existing
    WHERE existing."Code" = v."Code");

INSERT INTO "Species" ("Id", "Code", "Name")
SELECT v."Id"::uuid, v."Code", v."Name"
FROM (VALUES
    ('b3e4a5c0-0000-4000-8000-000000000001', 'BrownTrout', 'Brown Trout'),
    ('b3e4a5c0-0000-4000-8000-000000000002', 'RainbowTrout', 'Rainbow Trout'),
    ('b3e4a5c0-0000-4000-8000-000000000003', 'BrookTrout', 'Brook Trout'),
    ('b3e4a5c0-0000-4000-8000-000000000004', 'SeaTrout', 'Sea Trout'),
    ('b3e4a5c0-0000-4000-8000-000000000005', 'Salmon', 'Salmon'),
    ('b3e4a5c0-0000-4000-8000-000000000006', 'Pike', 'Pike'),
    ('b3e4a5c0-0000-4000-8000-000000000007', 'Perch', 'Perch'),
    ('b3e4a5c0-0000-4000-8000-000000000008', 'Carp', 'Carp'),
    ('b3e4a5c0-0000-4000-8000-000000000009', 'Bream', 'Bream'),
    ('b3e4a5c0-0000-4000-8000-00000000000a', 'Roach', 'Roach'),
    ('b3e4a5c0-0000-4000-8000-00000000000b', 'Tench', 'Tench'),
    ('b3e4a5c0-0000-4000-8000-00000000000c', 'Grayling', 'Grayling')
) AS v("Id", "Code", "Name")
WHERE NOT EXISTS (
    SELECT 1
    FROM "Species" existing
    WHERE existing."Code" = v."Code");
