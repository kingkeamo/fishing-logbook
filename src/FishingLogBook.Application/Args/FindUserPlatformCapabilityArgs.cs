using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Application.Args;

public sealed class FindUserPlatformCapabilityArgs
{
    public Guid UserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }
}
