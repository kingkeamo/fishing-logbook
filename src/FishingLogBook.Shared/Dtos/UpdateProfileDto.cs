namespace FishingLogBook.Shared.Dtos;

public sealed record UpdateProfileDto(
    string? DisplayName,
    string? HomeRegion,
    IReadOnlyList<string> PreferredFishingTypes,
    IReadOnlyList<string> PreferredSpecies,
    bool ShowDisplayName,
    bool ShowPhotograph,
    bool ShowHomeRegion,
    bool ShowPreferredFishingTypes,
    bool ShowPreferredSpecies);
