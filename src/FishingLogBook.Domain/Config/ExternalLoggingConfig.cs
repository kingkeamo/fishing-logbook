namespace FishingLogBook.Domain.Config;

public sealed class ExternalLoggingConfig
{
    public const string SectionName = "ExternalLogging";

    public const string GrafanaCloudProvider = "GrafanaCloud";

    public string Provider { get; set; } = "None";

    public string Url { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public bool IsConfigured =>
        string.Equals(Provider, GrafanaCloudProvider, StringComparison.OrdinalIgnoreCase) &&
        IsProvided(Url) &&
        IsProvided(ApiToken);

    public string StreamEnvironment
    {
        get
        {
            var value = Environment.Trim().ToLowerInvariant();
            return value is "localhost" or "dev" or "prod" ? value : "unknown";
        }
    }

    public string LokiBaseUrl
    {
        get
        {
            var url = Url.Trim().TrimEnd('/');
            const string pushPath = "/loki/api/v1/push";
            return url.EndsWith(pushPath, StringComparison.OrdinalIgnoreCase)
                ? url[..^pushPath.Length]
                : url;
        }
    }

    private static bool IsProvided(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Contains("user secrets", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains('<') &&
               !value.Contains('>');
    }
}
