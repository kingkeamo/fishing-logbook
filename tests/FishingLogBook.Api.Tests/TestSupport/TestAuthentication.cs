using FishingLogBook.Tests.Common.TestSupport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FishingLogBook.Api.Tests.TestSupport;

public static class TestAuthentication
{
    public static Dictionary<string, string?> Configuration { get; } = new()
    {
        ["Auth:Authority"] = TestJwt.Issuer,
        ["Auth:ClientId"] = TestJwt.ClientId,
        ["Auth:ApiScope"] = TestAuthConstants.ApiScope,
        ["Auth:ApiResource"] = TestAuthConstants.ApiResource
    };

    public static void ConfigureJwtBearer(IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = TestJwt.Issuer
            };
            configuration.SigningKeys.Add(TestJwt.SigningKey);
            options.Authority = string.Empty;
            options.MetadataAddress = string.Empty;
            options.RequireHttpsMetadata = false;
            options.Configuration = configuration;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            options.TokenValidationParameters.ValidIssuer = TestJwt.Issuer;
            options.TokenValidationParameters.IssuerSigningKey = TestJwt.SigningKey;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidAudience = TestAuthConstants.ApiResource;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            options.MapInboundClaims = false;
        });
    }
}
