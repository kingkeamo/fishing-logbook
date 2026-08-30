namespace FishingLogBook.Shared.Constants;

public static class AnglerLookupConstants
{
    public const int MinQueryLength = 3;

    public const int MaxQueryLength = 120;

    public const int MaxResults = 10;

    public static string? TrimQuery(string? query)
    {
        var trimmed = query?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool IsQueryValid(string? query)
    {
        var trimmed = TrimQuery(query);
        return trimmed is not null
            && trimmed.Length >= MinQueryLength
            && trimmed.Length <= MaxQueryLength;
    }
}
