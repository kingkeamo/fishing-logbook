namespace FishingLogBook.Web.Localization;

public static class CultureMatcher
{
    public static string Resolve(string? storedCulture, string? browserLanguage)
    {
        var fromStore = Match(storedCulture);
        if (fromStore is not null)
        {
            return fromStore;
        }

        return Match(browserLanguage) ?? CultureNames.English;
    }

    private static string? Match(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var language = value.Trim();
        if (language.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            return CultureNames.French;
        }

        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return CultureNames.English;
        }

        return null;
    }
}
