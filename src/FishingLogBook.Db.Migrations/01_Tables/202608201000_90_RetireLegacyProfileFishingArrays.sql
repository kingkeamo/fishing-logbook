DO $$
DECLARE
    legacy_row RECORD;
BEGIN
    FOR legacy_row IN
        SELECT "UserId", "PreferredFishingTypes", "PreferredSpecies"
        FROM "Profile"
        WHERE cardinality("PreferredFishingTypes") > 0
           OR cardinality("PreferredSpecies") > 0
    LOOP
        RAISE NOTICE
            'FLB#90: retiring legacy profile fishing arrays for UserId % — PreferredFishingTypes=%, PreferredSpecies=%. These do not map onto the FishingMethod/Species catalogue and are not migrated; use the FishingMethod/Species preference UI instead.',
            legacy_row."UserId", legacy_row."PreferredFishingTypes", legacy_row."PreferredSpecies";
    END LOOP;
END $$;

ALTER TABLE "Profile"
    RENAME COLUMN "ShowPreferredFishingTypes" TO "ShowPreferredFishingMethods";

ALTER TABLE "Profile"
    DROP COLUMN IF EXISTS "PreferredFishingTypes",
    DROP COLUMN IF EXISTS "PreferredSpecies";
