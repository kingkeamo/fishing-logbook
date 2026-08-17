using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Users;

public sealed class UserPlatformCapability
{
    public Guid UserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }

    public DateTimeOffset CreatedOn { get; init; }
}
