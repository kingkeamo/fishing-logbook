using System.Globalization;

namespace FishingLogBook.Mobile.Common.Offline.Sqlite;

internal static class SqliteDateTime
{
    public static string Format(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public static DateTimeOffset? ParseNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Parse(value);
    }
}
