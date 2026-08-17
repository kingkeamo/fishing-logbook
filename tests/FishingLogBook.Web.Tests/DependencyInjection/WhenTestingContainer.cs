using AwesomeAssertions;
using FishingLogBook.Tests.Common.TestSupport;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Web.Tests.DependencyInjection;

public class WhenTestingContainer : BaseDependencyInjectionTest
{
    [Fact]
    public void ItShouldBuildAndValidateTheServiceProvider()
    {
        // Arrange
        Action build = () =>
        {
            using var provider = CreateProvider();
        };

        // Act
        // Assert
        build.Should().NotThrow();
    }

    [Fact]
    public void ItShouldResolveServicesInjectedByComponents()
    {
        // Arrange
        var injectedTypes = GetComponentInjectedServiceTypes();

        // Act
        Action resolve = () =>
        {
            using var provider = CreateProvider();
            using var scope = provider.CreateScope();
            foreach (var type in injectedTypes)
            {
                scope.ServiceProvider.GetRequiredService(type);
            }
        };

        // Assert
        injectedTypes.Should().Contain(typeof(ISystemStatusClient));
        injectedTypes.Should().Contain(typeof(ITestCatchStore));
        injectedTypes.Should().Contain(typeof(ITestCatchPhotoStore));
        injectedTypes.Should().Contain(typeof(ITestCatchSynchroniser));
        injectedTypes.Should().Contain(typeof(IProfileClient));
        injectedTypes.Should().Contain(typeof(ICultureService));
        injectedTypes.Should().Contain(typeof(ILoggingService));
        injectedTypes.Should().Contain(typeof(ILocationService));
        injectedTypes.Should().Contain(typeof(ICatchClient));
        injectedTypes.Should().Contain(typeof(IStringLocalizer<UiStrings>));
        resolve.Should().NotThrow();
    }

    [Fact]
    public void ItShouldRegisterAuthorizedAndAnonymousHttpClients()
    {
        // Arrange
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // Act
        var apiClient = factory.CreateClient(HttpClientNames.AuthorizedApi);
        var anonymousClient = factory.CreateClient(HttpClientNames.Anonymous);

        // Assert
        apiClient.BaseAddress.Should().Be(new Uri("https://example.test/"));
        anonymousClient.BaseAddress.Should().Be(new Uri("https://example.test/"));
        scope.ServiceProvider.GetRequiredService<IAccessTokenProvider>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITestCatchClient>().Should().BeOfType<TestCatchClient>();
        var oidc = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<RemoteAuthenticationOptions<OidcProviderOptions>>>()
            .Value
            .ProviderOptions;
        oidc.ResponseType.Should().Be("code");
        oidc.ClientId.Should().Be("test-pwa-client");
        typeof(OidcProviderOptions).GetProperty("ClientSecret").Should().BeNull();
        oidc.DefaultScopes.Should().Contain("openid");
        oidc.DefaultScopes.Should().Contain("profile");
        oidc.DefaultScopes.Should().Contain("email");
        oidc.DefaultScopes.Should().Contain(TestAuthConstants.ApiScope);
        scope.ServiceProvider.GetRequiredService<ISignedInUserDisplayService>()
            .Should().BeOfType<SignedInUserDisplayService>();
    }

    [Fact]
    public void ItShouldRequestTheConfiguredApiResourceThroughStandardOidcOptions()
    {
        // Arrange
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        // Act
        var oidc = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<RemoteAuthenticationOptions<OidcProviderOptions>>>()
            .Value
            .ProviderOptions;

        // Assert
        oidc.AdditionalProviderParameters.Should().ContainKey("resource");
        oidc.AdditionalProviderParameters["resource"].Should().Be(TestAuthConstants.ApiResource);
        oidc.ResponseType.Should().Be("code");
        typeof(OidcProviderOptions).GetProperty("ClientSecret").Should().BeNull();
    }
}
