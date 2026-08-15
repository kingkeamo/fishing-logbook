INSERT INTO "SystemTest" ("Id", "Name", "CreatedOn")
SELECT gen_random_uuid(), 'FishingLogBook database online', now()
WHERE NOT EXISTS (SELECT 1 FROM "SystemTest");
