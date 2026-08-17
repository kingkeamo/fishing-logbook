using FluentResults;

namespace FishingLogBook.Application.Capabilities.Errors;

public sealed class MissingPlatformCapabilityError : Error
{
    public MissingPlatformCapabilityError()
        : base("The user does not have the required platform capability.")
    {
    }
}
