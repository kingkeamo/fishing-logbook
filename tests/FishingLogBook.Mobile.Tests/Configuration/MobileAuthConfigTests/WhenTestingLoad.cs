using AwesomeAssertions;
using FishingLogBook.Mobile.Configuration;

namespace FishingLogBook.Mobile.Tests.Configuration.MobileAuthConfigTests;

public class WhenTestingLoad
{
    [Fact]
    public void ItShouldLoadTheProvisionedDevelopmentNativeClientWithoutSecrets()
    {
        // Arrange
        // Act
        var config = MobileAuthConfig.Load();
        // Assert
        config.ClientId.Should().Be("513lhdbas693dqq2bs58v1ri8h");
        config.ApiBaseUrl.Should().Be("https://fishing-logbook-dev-api.fly.dev");
        config.ApiResource.Should().Be("https://fishing-logbook-dev-api.fly.dev");
        config.ApiScope.Should().Be("https://fishing-logbook-dev-api.fly.dev/access");
        config.Authority.Should().Be("https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_J4Kt9QB06");
        config.RedirectUri.Should().Be("https://fishing-logbook-dev.pages.dev/native/oauth2redirect");
        config.LogoutRedirectUri.Should().Be("https://fishing-logbook-dev.pages.dev/native/signedout");
        config.SchemeRedirectUri.Should().Be("com.fishinglogbook.mobile.demo://oauth2redirect");
        config.SchemeLogoutRedirectUri.Should().Be("com.fishinglogbook.mobile.demo://signedout");
        config.ApiBaseUrl.Should().NotContain("localhost");
        config.RedirectUri.Should().NotContain("localhost");
        config.GetType().GetProperties().Should().NotContain(property =>
            property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }
}
