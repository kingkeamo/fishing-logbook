using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Pages.SignIn;
using FishingLogBook.Web.Features.Authentication.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Authentication.Pages.SignInTests;

public class WhenTestingRender
{
    [Fact]
    public async Task ItShouldRedirectStraightToLoginWhenNoProvidersAreConfigured()
    {
        // Arrange
        await using var context = CreateContext([]);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/sign-in");

        // Act
        var cut = context.Render<SignIn>();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("authentication/login");
        cut.FindAll("#sign-in-chooser").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheChooserWhenProvidersAreConfigured()
    {
        // Arrange
        await using var context = CreateContext(["google"]);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/sign-in");

        // Act
        var cut = context.Render<SignIn>();

        // Assert
        cut.Find("#sign-in-google").TextContent.Trim().Should().Be("Continue with Google");
        cut.Find("#sign-in-email").TextContent.Trim().Should().Be("Continue with email");
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("authentication/login");
    }

    [Fact]
    public async Task ItShouldReturnToTheRequestedPageAfterTheChosenProvider()
    {
        // Arrange
        await using var context = CreateContext(["google"]);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/sign-in?returnUrl=" + Uri.EscapeDataString(navigation.BaseUri + "catches"));
        var cut = context.Render<SignIn>();

        // Act
        await cut.Find("#sign-in-google").ClickAsync();

        // Assert
        var state = ((BunitNavigationManager)navigation).History.First().Options.HistoryEntryState;
        state.Should().Contain("identity_provider").And.Contain("Google");
        state.Should().Contain("catches");
    }

    private static BunitContext CreateContext(string[] socialProviders)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(new AuthConfig { SocialProviders = socialProviders });
        context.Services.AddSingleton<ISignInNavigationService, SignInNavigationService>();
        return context;
    }
}
