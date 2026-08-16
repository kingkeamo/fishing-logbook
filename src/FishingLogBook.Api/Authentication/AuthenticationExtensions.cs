using System.Security.Claims;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FishingLogBook.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddFishingLogBookJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuthConfig>()
            .Bind(configuration.GetSection(AuthConfig.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthConfig>, AuthConfigStartupValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AuthConfig>>().Value);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<AuthConfig>(ConfigureJwtBearer);
        services.AddAuthorization();
        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, AuthConfig authConfig)
    {
        authConfig.EnsureRequired();
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

        options.Authority = authConfig.Authority;
        options.RequireHttpsMetadata = true;
        options.MetadataAddress = $"{authConfig.Authority.TrimEnd('/')}/.well-known/openid-configuration";
    }

    private static TokenValidationParameters CreateValidationParameters(AuthConfig authConfig)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authConfig.Authority,
            ValidateAudience = true,
            ValidAudience = authConfig.ApiResource,
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

        if (!HasScope(principal, authConfig.ApiScope))
        {
            context.Fail("Missing required scope.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(principal.FindFirst("sub")?.Value))
        {
            context.Fail("Missing subject.");
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

    private sealed class AuthConfigStartupValidator : IValidateOptions<AuthConfig>
    {
        public ValidateOptionsResult Validate(string? name, AuthConfig options)
        {
            try
            {
                options.EnsureRequired();
                return ValidateOptionsResult.Success;
            }
            catch (InvalidOperationException exception)
            {
                return ValidateOptionsResult.Fail(exception.Message);
            }
        }
    }
}
