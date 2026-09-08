namespace FishingLogBook.Domain.Config;

public sealed class GeoapifyConfig
{
    public const string SectionName = "Geoapify";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.geoapify.com/";

    public bool IsConfigured =>
        IsProvided(ApiKey) &&
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool IsProvided(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Contains("user secrets", StringComparison.OrdinalIgnoreCase);
    }
}
