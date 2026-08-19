using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Components.AppErrorBoundary;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Layouts.MainLayout;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingPageFailure : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldKeepTheApplicationChromeWhenTheBodyThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);
        var logging = context.Services.GetRequiredService<ILoggingService>();

        // Act
        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder =>
            {
                builder.OpenComponent<ThrowingComponent>(0);
                builder.CloseComponent();
            }));

        // Assert
        cut.Find("#app-menu-button").Should().NotBeNull();
        cut.Find("#record-catch-nav-link").Should().NotBeNull();
        cut.Find("#catch-logbook-nav-link").Should().NotBeNull();
        cut.Find("#theme-toggle-button").Should().NotBeNull();
        cut.Find("#app-brand").Should().NotBeNull();
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Is<Exception>(exception => exception.Message == "boom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPlaceTheErrorBoundaryInsideTheMainContentRatherThanAroundTheChrome()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder =>
                builder.AddMarkupContent(0, "<p id=\"page-body\">body</p>")));

        // Assert
        var boundary = cut.FindComponent<AppErrorBoundary>();
        boundary.Should().NotBeNull();
        cut.FindComponent<MudAppBar>().Should().NotBeNull();
        cut.FindComponent<MudDrawer>().Should().NotBeNull();
        boundary.FindAll("#app-menu-button").Should().BeEmpty();
        boundary.FindAll("#app-drawer").Should().BeEmpty();
        cut.Find("#app-shell-content").QuerySelector("#page-body").Should().NotBeNull();
    }

    private sealed class ThrowingComponent : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
