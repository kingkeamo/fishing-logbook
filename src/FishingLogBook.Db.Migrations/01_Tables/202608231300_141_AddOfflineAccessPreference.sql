ALTER TABLE "User"
ADD COLUMN "OfflineAccessEnabled" boolean NOT NULL DEFAULT false,
ADD COLUMN "OfflineAccessEnabledAt" timestamp with time zone NULL;
