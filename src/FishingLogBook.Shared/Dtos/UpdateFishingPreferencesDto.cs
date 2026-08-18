namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateFishingPreferencesDto(IReadOnlyList<UpdateFishingMethodPreferenceDto> Methods);
