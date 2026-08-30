using FishingLogBook.Application.Catches.Contracts.Services;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchPhotographObjectKeyBuilder : ICatchPhotographObjectKeyBuilder
{
    public string Build(Guid catchId, Guid photographId)
    {
        return $"catch-photographs/{catchId:D}/{photographId:D}";
    }
}
