namespace FishingLogBook.Api.Configuration;

public sealed class AuthConfig
{
    public const string SectionName = "Auth";

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ApiScope { get; set; } = string.Empty;

    public string ApiResource { get; set; } = string.Empty;

    public void EnsureRequired()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Authority))
        {
            missing.Add("Auth:Authority");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            missing.Add("Auth:ClientId");
        }

        if (string.IsNullOrWhiteSpace(ApiResource))
        {
            missing.Add("Auth:ApiResource");
        }

        if (string.IsNullOrWhiteSpace(ApiScope))
        {
            missing.Add("Auth:ApiScope");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Auth configuration is incomplete. Missing or empty: " + string.Join(", ", missing) + ".");
        }
    }
}
