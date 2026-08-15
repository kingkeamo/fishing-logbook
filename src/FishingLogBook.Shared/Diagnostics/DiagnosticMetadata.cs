namespace FishingLogBook.Shared.Diagnostics;

public static class DiagnosticMetadata
{
    public const string Operation = "operation";
    public const string ElapsedMilliseconds = "elapsedMilliseconds";
    public const string StoreName = "storeName";
    public const string RecordCount = "recordCount";
    public const string RetryCount = "retryCount";
    public const string HttpStatusCode = "httpStatusCode";
    public const string Platform = "platform";
    public const string Browser = "browser";
    public const string IsOnline = "isOnline";
    public const string QuotaBytes = "quotaBytes";
    public const string UsageBytes = "usageBytes";
    public const string TimeoutMilliseconds = "timeoutMilliseconds";
    public const string ErrorType = "errorType";
    public const string Result = "result";

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Operation,
        ElapsedMilliseconds,
        StoreName,
        RecordCount,
        RetryCount,
        HttpStatusCode,
        Platform,
        Browser,
        IsOnline,
        QuotaBytes,
        UsageBytes,
        TimeoutMilliseconds,
        ErrorType,
        Result
    };

    private static readonly string[] ForbiddenFragments =
    [
        "latitud",
        "longitud",
        "gps",
        "coord",
        "note",
        "photo",
        "image",
        "base64",
        "token",
        "password",
        "secret",
        "connection",
        "authoriz",
        "cookie",
        "cognito",
        "species"
    ];

    public static IReadOnlyDictionary<string, string> Filter(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var filtered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in metadata)
        {
            if (!IsAllowed(pair.Key) || pair.Value is null)
            {
                continue;
            }

            filtered[pair.Key] = Truncate(pair.Value, 120);
        }

        return filtered;
    }

    public static bool IsAllowed(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !AllowedKeys.Contains(key))
        {
            return false;
        }

        foreach (var fragment in ForbiddenFragments)
        {
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }
}
