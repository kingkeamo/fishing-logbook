ALTER TABLE "Profile"
    ADD COLUMN IF NOT EXISTS "OnboardingCompletedOn" timestamp with time zone NULL;
