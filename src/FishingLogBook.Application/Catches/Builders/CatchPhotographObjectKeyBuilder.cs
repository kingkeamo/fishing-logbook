using FishingLogBook.Application.Catches.Contracts.Builders;

namespace FishingLogBook.Application.Catches.Builders;

public sealed class CatchPhotographObjectKeyBuilder : ICatchPhotographObjectKeyBuilder
{
    public string Build(Guid catchId, Guid photographId)
    {
        return $"catch-photographs/{catchId:D}/{photographId:D}";
    }
}
