using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record AnglerPreferencesModel(
    FishingCatalogueDto Catalogue,
    FishingPreferencesDto Preferences,
    FishingLocationPreferencesDto Locations);
