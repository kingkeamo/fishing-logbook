namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateFishingMethodPreferenceDto(
    Guid FishingMethodId,
    bool IsDefault,
    IReadOnlyList<UpdateFishingSpeciesPreferenceDto> Species);
