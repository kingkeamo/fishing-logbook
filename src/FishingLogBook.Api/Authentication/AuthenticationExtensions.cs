using System.Security.Claims;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FishingLogBook.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddFishingLogBookJwtBearer(
        this IServiceCollection services,
        AuthConfig authConfig)
    {
        services.AddSingleton(authConfig);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearer(options, authConfig));
        services.AddAuthorization();
        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, AuthConfig authConfig)
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = CreateValidationParameters(authConfig);
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var config = context.HttpContext.RequestServices.GetRequiredService<AuthConfig>();
                return ValidateAccessToken(context, config);
            }
        };

        if (string.IsNullOrWhiteSpace(authConfig.Authority))
        {
            options.RequireHttpsMetadata = false;
            options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = "unconfigured"
            };
            return;
        }

        options.Authority = authConfig.Authority;
        options.RequireHttpsMetadata = authConfig.Authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        options.MetadataAddress = $"{authConfig.Authority.TrimEnd('/')}/.well-known/openid-configuration";
    }

    private static TokenValidationParameters CreateValidationParameters(AuthConfig authConfig)
    {
        var issuer = string.IsNullOrWhiteSpace(authConfig.Authority) ? "unconfigured" : authConfig.Authority;
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = string.IsNullOrWhiteSpace(authConfig.ApiResource)
                ? AuthConstants.DevApiResourceUri
                : authConfig.ApiResource,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    private static Task ValidateAccessToken(TokenValidatedContext context, AuthConfig authConfig)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Missing principal.");
            return Task.CompletedTask;
        }

        if (!HasClaim(principal, "token_use", AuthConstants.TokenUseAccess))
        {
            context.Fail("Access token required.");
            return Task.CompletedTask;
        }

        if (!HasClaim(principal, "client_id", authConfig.ClientId))
        {
            context.Fail("Invalid client.");
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(authConfig.ApiScope) && !HasScope(principal, authConfig.ApiScope))
        {
            context.Fail("Missing required scope.");
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static bool HasClaim(ClaimsPrincipal principal, string type, string expected)
    {
        var value = principal.FindFirst(type)?.Value;
        return string.Equals(value, expected, StringComparison.Ordinal);
    }

    private static bool HasScope(ClaimsPrincipal principal, string requiredScope)
    {
        var scopes = principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return scopes.Contains(requiredScope, StringComparer.Ordinal);
    }
}
