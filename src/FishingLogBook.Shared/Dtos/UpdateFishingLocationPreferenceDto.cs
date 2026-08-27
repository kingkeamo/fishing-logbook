namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateFishingLocationPreferenceDto(Guid Id, string Name, bool IsDefault);
