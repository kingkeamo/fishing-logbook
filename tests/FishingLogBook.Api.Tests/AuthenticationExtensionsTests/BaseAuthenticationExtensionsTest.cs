using FishingLogBook.Api.Authentication;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Tests.Common.TestSupport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Api.Tests.AuthenticationExtensionsTests;

public class BaseAuthenticationExtensionsTest
{
    protected static JwtBearerOptions CreateConfiguredJwtBearerOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = TestJwt.Issuer,
                ["Auth:ClientId"] = TestJwt.ClientId,
                ["Auth:ApiScope"] = TestAuthConstants.ApiScope,
                ["Auth:ApiResource"] = TestAuthConstants.ApiResource
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFishingLogBookJwtBearer(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }
}
