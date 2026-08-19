using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Layouts.MainLayout;
using FishingLogBook.Web.Localization;
using MudBlazor;
using MudBlazor.Extensions;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingResponsiveShell : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldUseAResponsiveDrawerAtTheMediumBreakpoint()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        var drawer = cut.FindComponent<MudDrawer>().Instance;
        drawer.Variant.Should().Be(DrawerVariant.Responsive);
        drawer.Breakpoint.Should().Be(Breakpoint.Md);
        drawer.ClipMode.Should().Be(DrawerClipMode.Always);
    }

    [Fact]
    public async Task ItShouldHideTheNavigationButtonFromTheMediumBreakpointUp()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        var menuButton = cut.Find("#app-menu-button");
        menuButton.ClassList.Should().Contain("d-md-none");
        menuButton.GetAttribute("aria-label").Should().Be("Open menu");
    }

    [Fact]
    public async Task ItShouldToggleTheDrawerFromTheNavigationButton()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);
        var cut = context.Render<MainLayout>();
        var openBefore = cut.FindComponent<MudDrawer>().Instance.GetState(x => x.Open);

        // Act
        await cut.Find("#app-menu-button").ClickAsync();

        // Assert
        openBefore.Should().BeFalse();
        cut.FindComponent<MudDrawer>().Instance.GetState(x => x.Open).Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldOwnTheGlobalContentGuttersRatherThanTheHostPage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder =>
                builder.AddMarkupContent(0, "<p id=\"page-body\">body</p>")));

        // Assert
        var shell = cut.Find("#app-shell-content");
        shell.ClassList.Should().Contain("app-shell-content");
        shell.QuerySelector("#page-body").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldKeepACompactBrandWithoutDuplicatingItInTheDrawer()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#app-brand-mark").Should().NotBeNull();
        cut.Find("#app-brand-name").ClassList.Should().Contain("d-none");
        cut.Find("#app-brand-name").ClassList.Should().Contain("d-sm-flex");
        cut.Find("#app-drawer").QuerySelectorAll("#app-brand-name").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderThePageBodyWithoutWaitingForTheProfileSummary()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var pending = new TaskCompletionSource<ProfileSummaryModel>();
        var profileSummary = Substitute.For<IProfileSummaryProvider>();
        profileSummary.GetAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        await using var context = CreateContext(isAuthenticated: true, profileSummary: profileSummary);

        // Act
        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder =>
                builder.AddMarkupContent(0, "<p id=\"page-body\">body</p>")));

        // Assert
        cut.Find("#page-body").TextContent.Should().Be("body");
        cut.Find("#user-menu-default-avatar").Should().NotBeNull();
        pending.SetResult(ProfileSummaryModel.Empty);
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
