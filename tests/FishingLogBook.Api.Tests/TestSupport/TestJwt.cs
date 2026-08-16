using System.Security.Cryptography;
using FishingLogBook.Shared.Constants;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FishingLogBook.Api.Tests.TestSupport;

public static class TestJwt
{
    public const string Issuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool";
    public const string ClientId = "test-pwa-client";
    public const string Subject = "test-subject";

    public static readonly RsaSecurityKey SigningKey = CreateSigningKey();

    public static string CreateAccessToken(
        string? issuer = null,
        string? clientId = null,
        string? tokenUse = null,
        string? scope = null,
        string? audience = null,
        bool includeAudience = true,
        DateTime? expires = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(15);
        var notBefore = expiresAt.AddMinutes(-30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = includeAudience ? audience ?? AuthConstants.DevApiResourceUri : null,
            NotBefore = notBefore,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Subject,
                ["token_use"] = tokenUse ?? AuthConstants.TokenUseAccess,
                ["client_id"] = clientId ?? ClientId,
                ["scope"] = scope ?? $"openid profile email {AuthConstants.ApiScope}"
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static RsaSecurityKey CreateSigningKey()
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa)
        {
            KeyId = "test-key"
        };
    }
}
