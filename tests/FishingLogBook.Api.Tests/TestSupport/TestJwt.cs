using System.Security.Cryptography;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.TestSupport;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FishingLogBook.Api.Tests.TestSupport;

public static class TestJwt
{
    public const string Issuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool";
    public const string ClientId = "test-pwa-client";
    public const string Subject = "test-subject";

    // FishingLogBook API contract requires email; default Cognito access tokens may omit it.
    public const string Email = "tester@example.test";

    public static readonly RsaSecurityKey SigningKey = CreateSigningKey();

    public static string CreateAccessToken(
        string? issuer = null,
        string? clientId = null,
        string? tokenUse = null,
        string? scope = null,
        string? audience = null,
        string? subject = null,
        string? email = null,
        bool includeAudience = true,
        bool includeSubject = true,
        bool includeEmail = true,
        DateTime? expires = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(15);
        var notBefore = expiresAt.AddMinutes(-30);
        var claims = new Dictionary<string, object>
        {
            ["token_use"] = tokenUse ?? AuthConstants.TokenUseAccess,
            ["client_id"] = clientId ?? ClientId,
            ["scope"] = scope ?? $"openid profile email {TestAuthConstants.ApiScope}"
        };
        if (includeSubject)
        {
            claims["sub"] = subject ?? Subject;
        }

        if (includeEmail)
        {
            claims["email"] = email ?? Email;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = includeAudience ? audience ?? TestAuthConstants.ApiResource : null,
            NotBefore = notBefore,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
            Claims = claims
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
