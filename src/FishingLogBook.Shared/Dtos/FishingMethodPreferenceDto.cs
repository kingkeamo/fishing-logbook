namespace FishingLogBook.Shared.Dtos;

public sealed record FishingMethodPreferenceDto(
    Guid FishingMethodId,
    string Code,
    string Name,
    bool IsDefault,
    IReadOnlyList<FishingSpeciesPreferenceDto> Species);
