using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Layouts.MainLayout;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingMenu : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldShowProfileAndExistingDestinationsInTheMenu()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();
        await cut.Find("#app-menu-button").ClickAsync();

        // Assert
        cut.Find("#app-menu-button").GetAttribute("aria-label").Should().Be("Open menu");
        cut.Find("#profile-nav-link").TextContent.Should().Contain("Profile");
        cut.Find("#profile-nav-link").GetAttribute("href").Should().Be("/profile");
        cut.Find("#install-nav-link").GetAttribute("href").Should().Be("/install");
        cut.Find("#record-catch-nav-link").GetAttribute("href").Should().Be("/catches/record");
        cut.Find("#import-catches-nav-link").GetAttribute("href").Should().Be("/import");
        cut.Find("#import-catches-nav-link").TextContent.Should().Contain("Import Catches");
        cut.Find("#catch-logbook-nav-link").GetAttribute("href").Should().Be("/catches");
        cut.Find("#trips-nav-link").GetAttribute("href").Should().Be("/trips");
        cut.Find("#trips-nav-link").TextContent.Should().Contain("Trips");
        cut.FindAll("#test-catch-nav-button").Should().BeEmpty();
        cut.Find("#diagnostics-nav-button").GetAttribute("href").Should().Be("/diagnostics");
        cut.Find("#home-nav-link").GetAttribute("href").Should().Be("/");
        cut.Find("#app-brand-mark").GetAttribute("src").Should().Be("images/brand/brand-mark-transparent.png");
        cut.Find("#app-brand-mark").GetAttribute("src").Should().NotBe("icon-192.png");
        cut.Find("#app-brand-name").TextContent.Should().Be("Catch But Don’t Forget");
    }

    [Fact]
    public async Task ItShouldShowFrenchProfileNav()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#profile-nav-link").TextContent.Should().Contain("Profil");
        cut.Find("#record-catch-nav-link").TextContent.Should().Contain("Enregistrer une prise");
        cut.Find("#import-catches-nav-link").TextContent.Should().Contain("Importer des prises");
        cut.Find("#catch-logbook-nav-link").TextContent.Should().Contain("Prises");
        cut.Find("#trips-nav-link").TextContent.Should().Contain("Sorties");
        cut.Find("#app-menu-button").GetAttribute("aria-label").Should().Be("Ouvrir le menu");
    }

    [Fact]
    public async Task ItShouldMarkImportActiveOnlyAtTheImportEntryRoute()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/import");

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#import-catches-nav-link").ClassList.Should().Contain("active");
        cut.Find("#catch-logbook-nav-link").ClassList.Should().NotContain("active");
    }
}
