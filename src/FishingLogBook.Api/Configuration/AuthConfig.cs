namespace FishingLogBook.Api.Configuration;

public sealed class AuthConfig
{
    public const string SectionName = "Auth";

    public string Authority { get; set; } = string.Empty;

    public string[] ApprovedClientIds { get; set; } = [];

    public string ApiScope { get; set; } = string.Empty;

    public string ApiResource { get; set; } = string.Empty;

    public void EnsureRequired()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Authority))
        {
            missing.Add("Auth:Authority");
        }

        if (ApprovedClientIds is null || ApprovedClientIds.Length == 0)
        {
            missing.Add("Auth:ApprovedClientIds");
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

        if (!IsAbsoluteHttpsUri(Authority))
        {
            throw new InvalidOperationException("Auth:Authority must be an absolute HTTPS URI.");
        }

        var approvedClientIds = ApprovedClientIds!;
        if (approvedClientIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Auth:ApprovedClientIds must not contain empty values.");
        }

        if (approvedClientIds.Distinct(StringComparer.Ordinal).Count() != approvedClientIds.Length)
        {
            throw new InvalidOperationException("Auth:ApprovedClientIds must not contain duplicate values.");
        }
    }

    public bool IsApprovedClientId(string? clientId)
    {
        return !string.IsNullOrWhiteSpace(clientId)
            && ApprovedClientIds.Contains(clientId, StringComparer.Ordinal);
    }

    private static bool IsAbsoluteHttpsUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }
}
