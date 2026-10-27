using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Support;

public static class TestAnglerPreferences
{
    public static readonly Guid FlyId = Guid.Parse("6cebaa41-de9c-4264-8034-03636b05f3c0");
    public static readonly Guid SpinningId = Guid.Parse("83bf17bb-a4ea-4e81-80c5-cc1d66a19192");
    public static readonly Guid SalmonId = Guid.Parse("7ff7ce2c-59da-451e-afcc-66bee96ee267");
    public static readonly Guid BrownTroutId = Guid.Parse("51e3798c-3d4d-438a-86fc-1b1ada032a83");
    public static readonly Guid CorribId = Guid.Parse("4d2b8c1e-7a8f-4e57-9d1c-2f0e6b3a9c55");
    public static readonly Guid MoyId = Guid.Parse("9a1f2e3d-4c5b-4a69-8778-695a4b3c2d1e");

    public static FishingCatalogueDto Catalogue()
    {
        return new FishingCatalogueDto(
            [new FishingMethodDto(FlyId, "Fly", "Fly"), new FishingMethodDto(SpinningId, "Spinning", "Spinning")],
            [new SpeciesDto(SalmonId, "Salmon", "Salmon"), new SpeciesDto(BrownTroutId, "BrownTrout", "Brown Trout")]);
    }

    public static FishingPreferencesDto Preferences()
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyId,
                "Fly",
                "Fly",
                true,
                [
                    new FishingSpeciesPreferenceDto(SalmonId, "Salmon", "Salmon", true),
                    new FishingSpeciesPreferenceDto(BrownTroutId, "BrownTrout", "Brown Trout", false)
                ]),
            new FishingMethodPreferenceDto(SpinningId, "Spinning", "Spinning", false, [])
        ]);
    }

    public static FishingLocationPreferencesDto Locations()
    {
        return new FishingLocationPreferencesDto(
        [
            new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true),
            new FishingLocationPreferenceDto(MoyId, "River Moy", false)
        ]);
    }

    public static AnglerPreferencesModel Create()
    {
        return new AnglerPreferencesModel(Catalogue(), Preferences(), Locations());
    }

    public static AnglerPreferencesModel Empty()
    {
        return new AnglerPreferencesModel(Catalogue(), new FishingPreferencesDto([]), new FishingLocationPreferencesDto([]));
    }
}
