namespace FishingLogBook.Domain.Catalogue;

public sealed class UserFishingMethodPreference
{
    public Guid UserId { get; init; }
    public Guid FishingMethodId { get; init; }
    public bool IsDefault { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
}
