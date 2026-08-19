using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Components.UserMenu;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.UserMenuTests;

public class WhenTestingMenuActions : BaseUserMenuTest
{
    [Fact]
    public async Task ItShouldOpenTheMenuFromTheAvatarActivator()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);
        var (cut, popover) = RenderMenu(context);

        // Act
        await cut.Find("#user-menu-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            popover.Find("#user-menu-profile").TextContent.Should().Contain("Profile");
            popover.Find("#user-menu-sign-out").TextContent.Should().Contain("Sign out");
        });
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheMenuFromTheKeyboard()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);
        var (cut, popover) = RenderMenu(context);

        // Act
        await cut.Find(".mud-menu-activator")
            .KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        // Assert
        cut.WaitForAssertion(() =>
        {
            popover.Find("#user-menu-profile").TextContent.Should().Contain("Profile");
            popover.Find("#user-menu-sign-out").TextContent.Should().Contain("Sign out");
        });
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferProfileAndSignOutInTheMenu()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);
        var (cut, popover) = RenderMenu(context);

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<MudMenu>().Instance.OpenMenuAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        // Assert
        cut.WaitForAssertion(() =>
        {
            popover.Find("#user-menu-profile").TextContent.Should().Contain("Profile");
            popover.Find("#user-menu-sign-out").TextContent.Should().Contain("Sign out");
        });
        popover.Find("#user-menu-profile").GetAttribute("href").Should().Be("/profile");
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchMenuCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);
        var (cut, popover) = RenderMenu(context);

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<MudMenu>().Instance.OpenMenuAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        // Assert
        cut.WaitForAssertion(() =>
            popover.Find("#user-menu-sign-out").TextContent.Should().Contain("Déconnexion"));
        cut.Find("#user-menu-button").GetAttribute("aria-label").Should().Be("Menu du compte");
    }

    [Fact]
    public async Task ItShouldSignOutThroughTheExistingAuthenticationFlow()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary(WithPhotograph());
        await using var context = CreateContext(profileSummary);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var (cut, popover) = RenderMenu(context);
        await cut.InvokeAsync(() => cut.FindComponent<MudMenu>().Instance.OpenMenuAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
        cut.WaitForAssertion(() => popover.Find("#user-menu-sign-out").Should().NotBeNull());

        // Act
        await popover.Find("#user-menu-sign-out").ClickAsync();

        // Assert
        navigation.Uri.Should().Be(navigation.BaseUri + "authentication/logout");
        profileSummary.Received(1).Invalidate();
    }
}
