using AwesomeAssertions;
using FishingLogBook.Mobile.Configuration;

namespace FishingLogBook.Mobile.Tests.Configuration.MobileAuthConfigTests;

public class WhenTestingEnsureRequired
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("native client")]
    [InlineData("native-client!")]
    public void ItShouldRejectAnEmptyOrInvalidNativeClientId(string clientId)
    {
        // Arrange
        var config = CreateValidConfig() with { ClientId = clientId };
        var action = config.EnsureRequired;
        // Act
        var assertion = action.Should().Throw<InvalidOperationException>();
        // Assert
        assertion.Which.Message.Should().Contain(nameof(MobileAuthConfig.ClientId));
    }

    [Theory]
    [InlineData("http://api.example.com", "com.fishinglogbook.mobile.demo://oauth2redirect")]
    [InlineData("https://api.example.com", "other.application://oauth2redirect")]
    [InlineData("https://api.example.com", "com.fishinglogbook.mobile.demo://wrong")]
    public void ItShouldRejectInsecureEndpointsOrUnregisteredCallbacks(
        string apiBaseUrl,
        string redirectUri)
    {
        // Arrange
        var config = CreateValidConfig() with
        {
            ApiBaseUrl = apiBaseUrl,
            ApiResource = apiBaseUrl,
            ApiScope = apiBaseUrl + "/access",
            RedirectUri = redirectUri
        };
        var action = config.EnsureRequired;
        // Act
        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldAcceptTheProvisionedDevelopmentConfiguration()
    {
        // Arrange
        var config = CreateValidConfig();
        var action = config.EnsureRequired;
        // Act
        // Assert
        action.Should().NotThrow();
    }

    private static MobileAuthConfig CreateValidConfig()
    {
        return new MobileAuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_J4Kt9QB06",
            ClientId = "513lhdbas693dqq2bs58v1ri8h",
            HostedUiDomain = "https://fishing-logbook-dev.auth.eu-west-2.amazoncognito.com",
            ApiBaseUrl = "https://fishing-logbook-dev-api.fly.dev",
            ApiScope = "https://fishing-logbook-dev-api.fly.dev/access",
            ApiResource = "https://fishing-logbook-dev-api.fly.dev",
            RedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            LogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
        };
    }
}
