namespace FishingLogBook.Shared.Constants;

public static class FishingLocationConstants
{
    public const int MaxNameLength = TripConstants.MaxPlaceNameLength;

    public static bool IsNameValid(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaxNameLength;
    }

    public static string? TrimName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool AreSameName(string? left, string? right)
    {
        return string.Equals(TrimName(left), TrimName(right), StringComparison.OrdinalIgnoreCase);
    }
}
