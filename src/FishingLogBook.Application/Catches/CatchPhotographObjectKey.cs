namespace FishingLogBook.Application.Catches;

internal static class CatchPhotographObjectKey
{
    public static string Build(Guid catchId, Guid photographId)
    {
        return $"catch-photographs/{catchId:D}/{photographId:D}";
    }
}
