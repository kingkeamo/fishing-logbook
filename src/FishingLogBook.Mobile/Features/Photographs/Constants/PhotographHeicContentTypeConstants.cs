namespace FishingLogBook.Mobile.Features.Photographs.Constants;

public static class PhotographHeicContentTypeConstants
{
    public const string Heic = "image/heic";

    public const string Heif = "image/heif";

    public static bool IsHeic(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return string.Equals(contentType, Heic, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, Heif, StringComparison.OrdinalIgnoreCase);
    }
}
