namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateFishingSpeciesPreferenceDto(Guid SpeciesId, bool IsDefault);
