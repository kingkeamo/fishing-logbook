namespace FishingLogBook.Shared.Dtos;

public sealed record FishingLocationPreferencesDto(IReadOnlyList<FishingLocationPreferenceDto> Locations);
