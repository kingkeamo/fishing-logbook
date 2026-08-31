UPDATE "Profile" AS profile
SET "DisplayName" = "User"."Email"
FROM "User"
WHERE "User"."Id" = profile."UserId"
  AND (profile."DisplayName" IS NULL OR profile."DisplayName" ~ '^[[:space:]]*$')
  AND "User"."Email" ~ '[^[:space:]]';
