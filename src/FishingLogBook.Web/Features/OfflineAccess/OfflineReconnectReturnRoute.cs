namespace FishingLogBook.Web.Features.OfflineAccess;

internal static class OfflineReconnectReturnRoute
{
    private const string DefaultRoute = "/catches";

    private static readonly IReadOnlyDictionary<string, string> KnownOfflineRoutes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["offline/catches"] = "/catches",
            ["offline/record"] = "/catches/record"
        };

    public static string Resolve(string? currentRelativePath)
    {
        var trimmed = currentRelativePath?.Trim().TrimStart('/');
        if (string.IsNullOrEmpty(trimmed))
        {
            return DefaultRoute;
        }

        return KnownOfflineRoutes.TryGetValue(trimmed, out var route) ? route : DefaultRoute;
    }
}
