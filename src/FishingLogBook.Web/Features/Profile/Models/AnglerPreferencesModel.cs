using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Profile.Models;

public sealed record AnglerPreferencesModel(
    FishingCatalogueDto Catalogue,
    FishingPreferencesDto Preferences,
    WeightUnitEnum WeightUnit,
    LengthUnitEnum LengthUnit)
{
    private readonly IReadOnlyList<FishingLocationPreferenceDto> _locations = [];

    public static AnglerPreferencesModel Empty { get; } = new(
        new FishingCatalogueDto([], []),
        new FishingPreferencesDto([]),
        WeightUnitEnum.Kg,
        LengthUnitEnum.Cm);

    public IReadOnlyList<FishingLocationPreferenceDto> Locations
    {
        get
        {
            return _locations;
        }
        init
        {
            _locations = value ?? [];
        }
    }

    public FishingLocationPreferenceDto? DefaultLocation
    {
        get
        {
            return Locations.FirstOrDefault(location => location.IsDefault);
        }
    }

    public bool HasCatalogue
    {
        get
        {
            return Catalogue.Methods.Count > 0 || Catalogue.AllSpecies.Count > 0;
        }
    }
}
