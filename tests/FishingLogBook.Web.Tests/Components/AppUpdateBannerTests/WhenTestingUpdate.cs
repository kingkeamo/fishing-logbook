using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Components.AppUpdateBanner;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Browser.Update.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Components.AppUpdateBannerTests;

public class WhenTestingUpdate : BaseAppUpdateBannerTest
{
    [Fact]
    public async Task ItShouldAskTheSharedServiceToUpdate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AppUpdateStatus.Available);
        await using var context = CreateContext(service);
        var cut = context.Render<AppUpdateBanner>();
        cut.WaitForAssertion(() => cut.Find("#app-update-banner-action"));

        // Act
        await cut.Find("#app-update-banner-action").ClickAsync();

        // Assert
        await service.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartMoreThanOneUpdateFromRepeatedTaps()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson };
        await using var context = CreateBrowserContext(js);
        var cut = context.Render<AppUpdateBanner>();
        cut.WaitForAssertion(() => cut.Find("#app-update-banner-action"));

        // Act
        await cut.Find("#app-update-banner-action").ClickAsync();
        cut.WaitForAssertion(() => cut.FindAll("#app-update-banner-action").Should().BeEmpty());

        // Assert
        js.Invocations.Count(invocation => invocation == "applyUpdate").Should().Be(1);
    }

    [Fact]
    public async Task ItShouldOfferTheUpdateWhenTheBrowserFindsOneAfterTheFirstRender()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.NoUpdateJson };
        await using var context = CreateBrowserContext(js);
        var cut = context.Render<AppUpdateBanner>();
        cut.WaitForAssertion(() => cut.FindAll("#app-update-banner").Should().BeEmpty());

        // Act
        js.Publish(FakeAppUpdateJsRuntime.UpdateReadyJson);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner-title").TextContent
                .Should().Contain("New CBDF version available");
            cut.Find("#app-update-banner-action").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldActivateTheWaitingVersionThroughTheBrowser()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson };
        await using var context = CreateBrowserContext(js);
        var cut = context.Render<AppUpdateBanner>();
        cut.WaitForAssertion(() => cut.Find("#app-update-banner-action"));

        // Act
        await cut.Find("#app-update-banner-action").ClickAsync();

        // Assert
        js.ImportedModules.Should().Contain("./js/browser/app-update.js");
        js.Invocations.Should().Contain("applyUpdate");
        cut.WaitForAssertion(() =>
            cut.Find("#app-update-banner-body").TextContent.Should().Contain("reopen"));
    }

    [Fact]
    public async Task ItShouldKeepManualUseAvailableWhenActivationFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var js = new FakeAppUpdateJsRuntime
        {
            StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson,
            ApplyAccepted = false
        };
        await using var context = CreateBrowserContext(js);
        var cut = context.Render<AppUpdateBanner>();
        cut.WaitForAssertion(() => cut.Find("#app-update-banner-action"));

        // Act
        await cut.Find("#app-update-banner-action").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner-body").TextContent.Should().Contain("keep using the app");
            cut.Find("#app-update-banner-action").Should().NotBeNull();
        });
    }
}
