namespace FishingLogBook.Domain.FishingLocations;

public sealed class UserFishingLocationPreference
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public DateTimeOffset CreatedOn { get; init; }
}
