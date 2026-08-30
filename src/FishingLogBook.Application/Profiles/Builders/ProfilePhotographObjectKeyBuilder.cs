using FishingLogBook.Application.Profiles.Contracts.Builders;

namespace FishingLogBook.Application.Profiles.Builders;

public sealed class ProfilePhotographObjectKeyBuilder : IProfilePhotographObjectKeyBuilder
{
    public string Build(Guid userId, Guid photographId)
    {
        return $"profiles/{userId:D}/{photographId:D}";
    }
}
