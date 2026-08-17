ALTER TABLE "Catch"
    DROP CONSTRAINT IF EXISTS "Catch_LocationVisibility_Allowed";

ALTER TABLE "Catch"
    ADD CONSTRAINT "Catch_LocationVisibility_Allowed" CHECK (
        "LocationVisibility" IS NULL
        OR "LocationVisibility" IN ('Private', 'Approximate', 'FishingVenueOnly', 'Public')
    );
