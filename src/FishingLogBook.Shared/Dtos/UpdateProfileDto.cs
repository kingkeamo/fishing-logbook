using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateProfileDto(
    string? DisplayName,
    string? HomeRegion,
    bool ShowDisplayName,
    bool ShowPhotograph,
    bool ShowHomeRegion,
    bool ShowPreferredFishingMethods,
    bool ShowPreferredSpecies,
    WeightUnitEnum PreferredWeightUnit = WeightUnitEnum.Kg,
    LengthUnitEnum PreferredLengthUnit = LengthUnitEnum.Cm);
