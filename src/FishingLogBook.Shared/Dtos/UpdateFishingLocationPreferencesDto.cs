namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateFishingLocationPreferencesDto(
    IReadOnlyList<UpdateFishingLocationPreferenceDto> Locations);
