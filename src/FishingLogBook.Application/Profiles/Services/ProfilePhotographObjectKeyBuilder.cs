using FishingLogBook.Application.Profiles.Contracts.Services;

namespace FishingLogBook.Application.Profiles.Services;

public sealed class ProfilePhotographObjectKeyBuilder : IProfilePhotographObjectKeyBuilder
{
    public string Build(Guid userId, Guid photographId)
    {
        return $"profiles/{userId:D}/{photographId:D}";
    }
}
