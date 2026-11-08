using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.Authentication.Services.SignInNavigationServiceTests;

public class WhenTestingSignInWith : BaseSignInNavigationServiceTest
{
    [Fact]
    public async Task ItShouldNotSendAProviderForEmailPasswordSignIn()
    {
        // Arrange
        await using var context = new BunitContext();
        var sut = CreateSut(context, "google");

        // Act
        sut.SignInWith(SignInProviderEnum.EmailPassword);

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().NotContain("identity_provider");
    }

    [Fact]
    public async Task ItShouldNotSendAProviderWhenEmailPasswordIsChosenFromTheChooser()
    {
        // Arrange
        await using var context = new BunitContext();
        var sut = CreateSut(context, "google");

        // Act
        sut.SignInWith(SignInProviderEnum.EmailPassword, "/catches");

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().NotContain("identity_provider");
    }

    [Theory]
    [InlineData(SignInProviderEnum.Google, "Google")]
    [InlineData(SignInProviderEnum.Apple, "SignInWithApple")]
    public async Task ItShouldRequestTheChosenProviderAndReturnToTheOriginatingPage(SignInProviderEnum provider, string expected)
    {
        // Arrange
        await using var context = new BunitContext();
        var sut = CreateSut(context, "google", "apple");

        // Act
        sut.SignInWith(provider, "/catches");

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        var state = navigation.History.First().Options.HistoryEntryState;
        state.Should().Contain("identity_provider").And.Contain(expected);
        state.Should().Contain("catches");
    }
}
