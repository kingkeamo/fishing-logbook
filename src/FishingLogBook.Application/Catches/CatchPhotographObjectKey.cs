namespace FishingLogBook.Application.Catches;

internal static class CatchPhotographObjectKey
{
    public static string Build(Guid userId, Guid catchId, Guid photographId)
    {
        return $"catches/{userId:D}/{catchId:D}/{photographId:D}";
    }
}
