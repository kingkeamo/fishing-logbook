using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record AnglerPreferencesModel(
    FishingCatalogueDto Catalogue,
    FishingPreferencesDto Preferences,
    FishingLocationPreferencesDto Locations)
{
    public static AnglerPreferencesModel Empty { get; } = new(
        new FishingCatalogueDto([], []),
        new FishingPreferencesDto([]),
        new FishingLocationPreferencesDto([]));
}
