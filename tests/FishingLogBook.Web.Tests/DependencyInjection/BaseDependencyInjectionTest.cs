using System.Reflection;
using FishingLogBook.Tests.Common.TestSupport;
using FishingLogBook.Web;
using FishingLogBook.Web.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DependencyInjection;

public class BaseDependencyInjectionTest
{
    protected static ServiceProvider CreateProvider(AuthConfig? authConfig = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddFishingLogBookWeb(
            new ApiConfig { BaseUrl = "https://example.test/" },
            new DiagnosticsClientConfig(),
            authConfig ?? CreateCompleteAuthConfig(),
            new Uri("https://example.test/"));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    protected static AuthConfig CreateCompleteAuthConfig()
    {
        return new AuthConfig
        {
            Authority = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool",
            ClientId = "test-pwa-client",
            ApiScope = TestAuthConstants.ApiScope,
            ApiResource = TestAuthConstants.ApiResource
        };
    }

    protected static IReadOnlyCollection<Type> GetComponentInjectedServiceTypes()
    {
        return typeof(App).Assembly
            .GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(property => property.PropertyType)
            .Distinct()
            .ToArray();
    }
}
