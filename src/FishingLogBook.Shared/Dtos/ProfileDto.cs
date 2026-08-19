using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Dtos;

public sealed record ProfileDto(
    Guid UserId,
    string? DisplayName,
    Guid? PhotographId,
    string? PhotographUrl,
    string? PhotographContentType,
    string? HomeRegion,
    IReadOnlyList<string> PreferredFishingTypes,
    IReadOnlyList<string> PreferredSpecies,
    bool ShowDisplayName,
    bool ShowPhotograph,
    bool ShowHomeRegion,
    bool ShowPreferredFishingTypes,
    bool ShowPreferredSpecies,
    WeightUnitEnum PreferredWeightUnit = WeightUnitEnum.Kg,
    LengthUnitEnum PreferredLengthUnit = LengthUnitEnum.Cm);
