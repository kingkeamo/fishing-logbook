using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Profile.Models;

public sealed record AnglerPreferencesModel(
    FishingCatalogueDto Catalogue,
    FishingPreferencesDto Preferences,
    WeightUnitEnum WeightUnit,
    LengthUnitEnum LengthUnit)
{
    public static AnglerPreferencesModel Empty { get; } = new(
        new FishingCatalogueDto([], []),
        new FishingPreferencesDto([]),
        WeightUnitEnum.Kg,
        LengthUnitEnum.Cm);

    public bool HasCatalogue
    {
        get
        {
            return Catalogue.Methods.Count > 0 || Catalogue.AllSpecies.Count > 0;
        }
    }
}
