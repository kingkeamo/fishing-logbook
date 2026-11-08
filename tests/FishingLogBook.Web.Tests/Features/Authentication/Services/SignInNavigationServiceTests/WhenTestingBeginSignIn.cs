using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.Authentication.Services.SignInNavigationServiceTests;

public class WhenTestingBeginSignIn : BaseSignInNavigationServiceTest
{
    [Fact]
    public async Task ItShouldGoStraightToLoginWhenNoProvidersAreConfigured()
    {
        // Arrange
        await using var context = new BunitContext();
        var sut = CreateSut(context);

        // Act
        sut.BeginSignIn();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("authentication/login");
    }

    [Fact]
    public async Task ItShouldOpenTheChooserAndRememberTheCurrentPageWhenProvidersAreConfigured()
    {
        // Arrange
        await using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/catches");
        var sut = CreateSut(context, "google");

        // Act
        sut.BeginSignIn();

        // Assert
        navigation.Uri.Should().Contain("/sign-in?returnUrl=");
        navigation.Uri.Should().Contain(Uri.EscapeDataString(navigation.BaseUri + "catches"));
        navigation.Uri.Should().NotContain("authentication/login");
    }

    [Fact]
    public async Task ItShouldKeepAnExplicitSameSiteReturnRoute()
    {
        // Arrange
        await using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var sut = CreateSut(context, "google");

        // Act
        sut.BeginSignIn("/catches/record");

        // Assert
        navigation.Uri.Should().Contain(Uri.EscapeDataString(navigation.BaseUri + "catches/record"));
    }

    [Fact]
    public async Task ItShouldNotCarryAnExternalReturnUrlThroughSignIn()
    {
        // Arrange
        await using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var sut = CreateSut(context, "google");

        // Act
        sut.BeginSignIn("https://evil.example/phish");

        // Assert
        navigation.Uri.Should().NotContain("evil.example");
        navigation.Uri.Should().Contain(Uri.EscapeDataString(navigation.BaseUri));
    }
}
