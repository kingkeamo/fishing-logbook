using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Args;

public sealed class UpdateProfileArgs
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public string? HomeRegion { get; init; }

    public IReadOnlyList<string> PreferredFishingTypes { get; init; } = [];

    public IReadOnlyList<string> PreferredSpecies { get; init; } = [];

    public bool ShowDisplayName { get; init; } = true;

    public bool ShowPhotograph { get; init; }

    public bool ShowHomeRegion { get; init; }

    public bool ShowPreferredFishingTypes { get; init; }

    public bool ShowPreferredSpecies { get; init; }

    public CatchLocationDto? Location { get; init; }
}
