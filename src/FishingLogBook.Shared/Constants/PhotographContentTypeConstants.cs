namespace FishingLogBook.Shared.Constants;

public static class PhotographContentTypeConstants
{
    public const string Jpeg = "image/jpeg";

    public const string Png = "image/png";

    public const string Webp = "image/webp";

    public static readonly IReadOnlyList<string> Allowed =
    [
        Jpeg,
        Png,
        Webp
    ];

    public static bool IsAllowed(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return Allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }
}
