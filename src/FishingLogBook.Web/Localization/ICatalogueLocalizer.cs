using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Localization;

public interface ICatalogueLocalizer
{
    string GetSpeciesLabel(SpeciesDto species);

    string GetFishingMethodLabel(FishingMethodDto method);

    FishingCatalogueDto Localize(FishingCatalogueDto catalogue);

    FishingPreferencesDto Localize(FishingPreferencesDto preferences);
}
