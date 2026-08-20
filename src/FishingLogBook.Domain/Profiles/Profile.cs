using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Profiles;

public sealed class Profile
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public Guid? PhotographId { get; init; }

    public string? PhotographObjectKey { get; init; }

    public string? PhotographContentType { get; init; }

    public string? HomeRegion { get; init; }

    public WeightUnitEnum PreferredWeightUnit { get; init; } = WeightUnitEnum.Kg;

    public LengthUnitEnum PreferredLengthUnit { get; init; } = LengthUnitEnum.Cm;

    public bool ShowDisplayName { get; init; } = true;

    public bool ShowPhotograph { get; init; }

    public bool ShowHomeRegion { get; init; }

    public bool ShowPreferredFishingMethods { get; init; }

    public bool ShowPreferredSpecies { get; init; }

    public DateTimeOffset? OnboardingCompletedOn { get; init; }
}
