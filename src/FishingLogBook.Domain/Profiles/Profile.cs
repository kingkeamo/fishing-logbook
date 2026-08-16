namespace FishingLogBook.Domain.Profiles;

public sealed class Profile
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public Guid? PhotographId { get; init; }

    public string? PhotographObjectKey { get; init; }

    public string? PhotographContentType { get; init; }

    public string? HomeRegion { get; init; }

    public string[] PreferredFishingTypes { get; init; } = [];

    public string[] PreferredSpecies { get; init; } = [];

    public bool ShowDisplayName { get; init; } = true;

    public bool ShowPhotograph { get; init; }

    public bool ShowHomeRegion { get; init; }

    public bool ShowPreferredFishingTypes { get; init; }

    public bool ShowPreferredSpecies { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? LocationAccuracyMetres { get; init; }

    public DateTimeOffset? LocationCapturedOn { get; init; }

    public string? LocationSource { get; init; }

    public string? LocationVisibility { get; init; }

    public string? LocationConsentVersion { get; init; }
}
