using AwesomeAssertions;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.SignInProviderServiceTests;

public class WhenTestingAvailableProviders
{
    [Fact]
    public void ItShouldOfferNoSocialProvidersWhenNoneAreConfigured()
    {
        // Arrange
        // Act
        var sut = CreateSut("iOS");

        // Assert
        sut.AvailableProviders.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldIgnoreUnsupportedAndDuplicateConfiguredProviders()
    {
        // Arrange
        // Act
        var sut = CreateSut("Android", "facebook", "google", "Google", "");

        // Assert
        sut.AvailableProviders.Should().Equal(SignInProviderEnum.Google);
    }

    [Fact]
    public void ItShouldNotOfferAppleOnAndroid()
    {
        // Arrange
        // Act
        var sut = CreateSut("Android", "apple", "google");

        // Assert
        sut.AvailableProviders.Should().Equal(SignInProviderEnum.Google);
    }

    [Fact]
    public void ItShouldNotOfferAnyProviderOnAnUnsupportedPlatform()
    {
        // Arrange
        // Act
        var sut = CreateSut("WinUI", "apple", "google");

        // Assert
        sut.AvailableProviders.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldOfferOnlyConfiguredProvidersOnIos()
    {
        // Arrange
        // Act
        var appleOnly = CreateSut("iOS", "apple");
        var both = CreateSut("iOS", "google", "apple");

        // Assert
        appleOnly.AvailableProviders.Should().Equal(SignInProviderEnum.Apple);
        both.AvailableProviders.Should().Equal(SignInProviderEnum.Apple, SignInProviderEnum.Google);
    }

    private static SignInProviderService CreateSut(string platform, params string[] socialProviders)
    {
        var appInfo = Substitute.For<IAppInfoAdapter>();
        appInfo.Platform.Returns(platform);
        var config = new MobileAuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_pool",
            ClientId = "client123",
            HostedUiDomain = "https://auth.example.com",
            ApiBaseUrl = "https://api.example.com",
            ApiScope = "https://api.example.com/access",
            ApiResource = "https://api.example.com",
            RedirectUri = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect",
            LogoutRedirectUri = "https://fishing-logbook-dev.pages.dev/native/signedout",
            SchemeRedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            SchemeLogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout",
            SocialProviders = socialProviders
        };
        return new SignInProviderService(config, appInfo);
    }
}
