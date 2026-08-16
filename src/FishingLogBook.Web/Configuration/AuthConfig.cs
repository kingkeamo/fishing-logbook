namespace FishingLogBook.Web.Configuration;

public sealed class AuthConfig
{
    public const string SectionName = "Auth";

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ApiScope { get; set; } = string.Empty;

    public string ApiResource { get; set; } = string.Empty;
}
