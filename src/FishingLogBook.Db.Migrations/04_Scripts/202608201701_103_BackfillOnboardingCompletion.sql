INSERT INTO "Profile" ("UserId", "OnboardingCompletedOn")
SELECT "Id", now()
FROM "User"
ON CONFLICT ("UserId") DO NOTHING;

UPDATE "Profile"
SET "OnboardingCompletedOn" = now()
WHERE "OnboardingCompletedOn" IS NULL;
