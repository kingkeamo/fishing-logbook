namespace FishingLogBook.Shared.Dtos;

public sealed record FishingPreferencesDto(IReadOnlyList<FishingMethodPreferenceDto> Methods);
