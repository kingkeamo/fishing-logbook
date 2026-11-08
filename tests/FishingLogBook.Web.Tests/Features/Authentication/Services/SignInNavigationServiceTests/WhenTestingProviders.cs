using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Authentication.Services.SignInNavigationServiceTests;

public class WhenTestingProviders : BaseSignInNavigationServiceTest
{
    [Fact]
    public async Task ItShouldOfferNoProvidersWhenNoneAreConfigured()
    {
        // Arrange
        await using var context = new BunitContext();

        // Act
        var sut = CreateSut(context);

        // Assert
        sut.Providers.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreUnsupportedAndDuplicateProviders()
    {
        // Arrange
        await using var context = new BunitContext();

        // Act
        var sut = CreateSut(context, "facebook", "Google", "google", "");

        // Assert
        sut.Providers.Should().Equal(SignInProviderEnum.Google);
    }

    [Fact]
    public async Task ItShouldListAppleBeforeGoogle()
    {
        // Arrange
        await using var context = new BunitContext();

        // Act
        var sut = CreateSut(context, "google", "apple");

        // Assert
        sut.Providers.Should().Equal(SignInProviderEnum.Apple, SignInProviderEnum.Google);
    }
}
