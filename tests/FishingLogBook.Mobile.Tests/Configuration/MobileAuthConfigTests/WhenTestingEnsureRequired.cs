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
    [InlineData("http://api.example.com", "https://fishing-logbook-dev.pages.dev/native/oauth2redirect")]
    [InlineData("https://api.example.com", "com.fishinglogbook.mobile.demo://oauth2redirect")]
    [InlineData("https://api.example.com", "http://fishing-logbook-dev.pages.dev/native/oauth2redirect")]
    [InlineData("https://api.example.com", "https://fishing-logbook-dev.pages.dev/oauth2redirect")]
    [InlineData("https://api.example.com", "https://fishing-logbook-dev.pages.dev:8443/native/oauth2redirect")]
    [InlineData("https://api.example.com", "https://fishing-logbook-dev.pages.dev/native/oauth2redirect?next=x")]
    [InlineData("https://api.example.com", "https://user@fishing-logbook-dev.pages.dev/native/oauth2redirect")]
    [InlineData("https://api.example.com", "https://other.pages.dev/native/oauth2redirect")]
    public void ItShouldRejectInsecureEndpointsOrUnverifiedCallbacks(
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

    [Theory]
    [InlineData("other.application://oauth2redirect", "com.fishinglogbook.mobile.demo://signedout")]
    [InlineData("com.fishinglogbook.mobile.demo://wrong", "com.fishinglogbook.mobile.demo://signedout")]
    [InlineData("com.fishinglogbook.mobile.demo://oauth2redirect", "https://fishing-logbook-dev.pages.dev/native/signedout")]
    public void ItShouldRejectUnregisteredSchemeCallbacks(string schemeRedirectUri, string schemeLogoutRedirectUri)
    {
        // Arrange
        var config = CreateValidConfig() with
        {
            SchemeRedirectUri = schemeRedirectUri,
            SchemeLogoutRedirectUri = schemeLogoutRedirectUri
        };
        var action = config.EnsureRequired;
        // Act
        var assertion = action.Should().Throw<InvalidOperationException>();
        // Assert
        assertion.Which.Message.Should().StartWith("Scheme");
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

    [Fact]
    public void ItShouldSelectTheSchemeCallbacksForPlatformsWithoutVerifiedHttpsCallbacks()
    {
        // Arrange
        var config = CreateValidConfig();
        // Act
        var result = config.WithSchemeCallbacks();
        // Assert
        result.RedirectUri.Should().Be("com.fishinglogbook.mobile.demo://oauth2redirect");
        result.LogoutRedirectUri.Should().Be("com.fishinglogbook.mobile.demo://signedout");
        result.ClientId.Should().Be(config.ClientId);
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
            RedirectUri = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect",
            LogoutRedirectUri = "https://fishing-logbook-dev.pages.dev/native/signedout",
            SchemeRedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            SchemeLogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
        };
    }
}
