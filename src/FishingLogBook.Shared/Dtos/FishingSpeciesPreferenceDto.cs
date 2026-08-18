namespace FishingLogBook.Shared.Dtos;

public sealed record FishingSpeciesPreferenceDto(
    Guid SpeciesId,
    string Code,
    string Name,
    bool IsDefault);
