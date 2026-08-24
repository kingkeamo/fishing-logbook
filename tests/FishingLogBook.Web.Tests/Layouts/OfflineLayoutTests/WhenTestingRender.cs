using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Layouts.OfflineLayout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Layouts.OfflineLayoutTests;

public class WhenTestingRender : BaseOfflineLayoutTest
{
    [Fact]
    public async Task ItShouldUseTheSharedBrandingAndOnlyOfflineNavigation()
    {
        // Arrange
        await using var context = CreateContext(out _);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(layout => layout.Body, builder => builder.AddContent(0, "Offline content")));

        // Assert
        cut.Find("#app-brand-mark").Should().NotBeNull();
        cut.Find("#language-menu-button").Should().NotBeNull();
        cut.Find("#theme-toggle-button").Should().NotBeNull();
        cut.Find("#offline-catches-nav-link").Should().NotBeNull();
        cut.Find("#offline-record-nav-link").Should().NotBeNull();
        cut.FindAll("#profile-nav-link").Should().BeEmpty();
        cut.FindAll("#app-update-banner").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLockOnlyTheInMemoryOfflineContextAndReturnToLanding()
    {
        // Arrange
        await using var context = CreateContext(out var owner);
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(_ => { })));

        // Act
        await cut.Find("#offline-lock-nav-link").ClickAsync();

        // Assert
        owner.IsUnlocked.Should().BeFalse();
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/");
    }
}
