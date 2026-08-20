namespace FishingLogBook.Shared.Dtos;

public sealed record PublicProfileDto(
    Guid UserId,
    string? DisplayName,
    string? PhotographUrl,
    string? HomeRegion,
    IReadOnlyList<string> PreferredFishingMethods,
    IReadOnlyList<string> PreferredSpecies);
