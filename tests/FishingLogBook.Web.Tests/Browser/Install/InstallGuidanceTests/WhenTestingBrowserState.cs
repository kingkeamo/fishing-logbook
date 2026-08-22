using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Browser.Install.TestSupport;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallGuidanceTests;

public class WhenTestingBrowserState : BaseInstallGuidanceTest
{
    private const string IosSafariStateJson =
        """{"isInstalled":false,"canPrompt":false,"platformFamily":"iOS","isSafari":true}""";

    private const string AndroidPromptableStateJson =
        """{"isInstalled":false,"canPrompt":true,"platformFamily":"Android","isSafari":false}""";

    private const string InstalledAndroidStateJson =
        """{"isInstalled":true,"canPrompt":false,"platformFamily":"Android","isSafari":false}""";

    [Fact]
    public async Task ItShouldShowCompleteManualGuidanceWhenTheBrowserModuleFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var jsRuntime = new FakeInstallJsRuntime
        {
            StateFailure = new InvalidOperationException("module unavailable")
        };
        await using var context = CreateBrowserContext(jsRuntime);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-iphone-steps").Children.Should().HaveCount(8);
            cut.Find("#install-guidance-ipad-steps").Children.Should().HaveCount(6);
            cut.Find("#install-guidance-android-steps").Children.Should().HaveCount(4);
            cut.Find("#install-guidance-computer-steps").Children.Should().HaveCount(4);
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
        });
        jsRuntime.ImportedModules.Should().Contain("./js/browser/install.js");
    }

    [Theory]
    [InlineData(IosSafariStateJson, "install-guidance-ios-panel")]
    [InlineData(AndroidPromptableStateJson, "install-guidance-android-panel")]
    [InlineData(
        """{"isInstalled":false,"canPrompt":false,"platformFamily":"Desktop","isSafari":false}""",
        "install-guidance-computer-panel")]
    public async Task ItShouldExpandTheSectionForTheStateReturnedByTheBrowser(
        string stateJson,
        string expectedPanelId)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var jsRuntime = new FakeInstallJsRuntime { StateJson = stateJson };
        await using var context = CreateBrowserContext(jsRuntime);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            IsPanelExpanded(cut, expectedPanelId).Should().BeTrue();
            cut.Find("#install-guidance-ios-steps").Should().NotBeNull();
            cut.Find("#install-guidance-android-steps").Should().NotBeNull();
            cut.Find("#install-guidance-computer-steps").Should().NotBeNull();
        });
        jsRuntime.Invocations.Should().Contain("getInstallState").And.Contain("subscribeInstallState");
    }

    [Fact]
    public async Task ItShouldShowTheInstalledStateReturnedByTheBrowser()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var jsRuntime = new FakeInstallJsRuntime { StateJson = InstalledAndroidStateJson };
        await using var context = CreateBrowserContext(jsRuntime);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("App is installed");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.FindAll("#install-guidance-benefit").Should().BeEmpty();
        });
        jsRuntime.Invocations.Should().Contain("getInstallState");
    }

    [Fact]
    public async Task ItShouldUpdateTheMountedPageWhenTheBrowserPublishesANewState()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var jsRuntime = new FakeInstallJsRuntime { StateJson = AndroidPromptableStateJson };
        await using var context = CreateBrowserContext(jsRuntime);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        // Act
        await jsRuntime.PublishAsync(InstalledAndroidStateJson);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("App is installed");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.Find("#install-guidance-android-steps").Should().NotBeNull();
        });
        jsRuntime.UnsubscribedTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldUnsubscribeFromTheBrowserWhenThePageIsDisposed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var jsRuntime = new FakeInstallJsRuntime { StateJson = AndroidPromptableStateJson };
        var context = CreateBrowserContext(jsRuntime);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        // Act
        await context.DisposeAsync();

        // Assert
        jsRuntime.UnsubscribedTokens.Should().Equal(jsRuntime.SubscriptionToken);
        jsRuntime.Invocations.Should().Contain("unsubscribeInstallState");
    }
}
