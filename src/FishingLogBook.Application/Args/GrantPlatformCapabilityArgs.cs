using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Application.Args;

public sealed class GrantPlatformCapabilityArgs
{
    public Guid TargetUserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }
}
