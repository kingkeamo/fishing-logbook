using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallGuidanceTests;

public class WhenTestingRender : BaseInstallGuidanceTest
{
    [Fact]
    public async Task ItShouldShowCompleteManualGuidanceWhenDetectionFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var service = CreateService(InstallState.Unknown);
        service.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns<InstallState>(_ => throw new InvalidOperationException("interop failed"));
        await using var context = CreateContext(service, logging);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-ios-steps").Children.Should().HaveCount(5);
            cut.Find("#install-guidance-android-steps").Children.Should().HaveCount(4);
            cut.Find("#install-guidance-samsung-steps").Children.Should().HaveCount(3);
            cut.Find("#install-guidance-computer-steps").Children.Should().HaveCount(4);
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.FindAll("#install-guidance-loading").Should().BeEmpty();
        });
        await logging.Received(1).LogErrorAsync(
            "install detection",
            Arg.Is<Exception>(exception => exception.Message == "interop failed"),
            Arg.Any<CancellationToken>());
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
        await service.DidNotReceive().PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotGuessAPlatformForAnUnknownBrowser()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(InstallState.Unknown);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            IsPanelExpanded(cut, "install-guidance-ios-panel").Should().BeFalse();
            IsPanelExpanded(cut, "install-guidance-android-panel").Should().BeFalse();
            IsPanelExpanded(cut, "install-guidance-computer-panel").Should().BeFalse();
            cut.Find("#install-guidance-manual-intro").TextContent.Should().Contain("Choose your device");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InstallPlatformFamilies.Ios)]
    [InlineData(InstallPlatformFamilies.Android)]
    [InlineData(InstallPlatformFamilies.Desktop)]
    [InlineData(InstallPlatformFamilies.Other)]
    public async Task ItShouldKeepEveryPlatformSectionAvailableWithoutANativePrompt(string platformFamily)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(new InstallState(false, false, platformFamily, false));
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-ios-panel").TextContent.Should().Contain("iPhone / iPad");
            cut.Find("#install-guidance-ios-steps").TextContent.Should()
                .Contain("Safari").And.Contain("Share").And.Contain("Add to Home Screen");
            cut.Find("#install-guidance-ios-fallback").TextContent.Should().Contain("Open in Safari");
            cut.Find("#install-guidance-android-steps").TextContent.Should()
                .Contain("⋮").And.Contain("Install app").And.Contain("Add to Home screen");
            cut.Find("#install-guidance-samsung-heading").TextContent.Should().Contain("Samsung Internet");
            cut.Find("#install-guidance-samsung-steps").TextContent.Should()
                .Contain("Add page to").And.Contain("Home screen");
            cut.Find("#install-guidance-computer-steps").TextContent.Should()
                .Contain("address bar").And.Contain("Install this site as an app");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InstallPlatformFamilies.Ios, "install-guidance-ios-panel")]
    [InlineData(InstallPlatformFamilies.Android, "install-guidance-android-panel")]
    [InlineData(InstallPlatformFamilies.Desktop, "install-guidance-computer-panel")]
    public async Task ItShouldExpandOnlyTheDetectedPlatformSection(string platformFamily, string expectedPanelId)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(new InstallState(false, false, platformFamily, false));
        var panelIds = new[]
        {
            "install-guidance-ios-panel",
            "install-guidance-android-panel",
            "install-guidance-computer-panel"
        };
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            foreach (var panelId in panelIds)
            {
                IsPanelExpanded(cut, panelId).Should().Be(panelId == expectedPanelId);
            }
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTellIosUsersInAnotherBrowserToSwitchToSafari()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(new InstallState(false, false, InstallPlatformFamilies.Ios, false));
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-ios-browser").TextContent.Should().Contain("Safari");
            cut.Find("#install-guidance-ios-steps").Children.Should().HaveCount(5);
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRepeatTheSafariWarningForSafariUsers()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(IosSafari);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#install-guidance-ios-browser").Should().BeEmpty();
            cut.Find("#install-guidance-ios-fallback-heading").TextContent
                .Should().Contain("Add to Home Screen");
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAskAnInstalledDeviceToInstallAgain()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(new InstallState(true, false, InstallPlatformFamilies.Android, false));
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("App is installed");
            cut.Find("#install-guidance-other-devices").TextContent.Should().Contain("another phone");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.FindAll("#install-guidance-benefit").Should().BeEmpty();
            cut.FindAll("#install-guidance-manual-intro").Should().BeEmpty();
            cut.Find("#install-guidance-android-steps").Should().NotBeNull();
            IsPanelExpanded(cut, "install-guidance-android-panel").Should().BeFalse();
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
        await service.DidNotReceive().PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDescribeAnInstalledComputerApp()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(new InstallState(true, false, InstallPlatformFamilies.Desktop, false));
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("Start menu"));
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferTheNativeInstallButtonWhenTheBrowserCapturedAPrompt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(new InstallState(false, true, InstallPlatformFamilies.Android, false));
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-action").TextContent.Should().Contain("Install app");
            cut.Find("#install-guidance-android-steps").Should().NotBeNull();
            cut.Find("#install-guidance-ios-steps").Should().NotBeNull();
            cut.Find("#install-guidance-computer-steps").Should().NotBeNull();
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
        await service.Received(1).SubscribeAsync(
            Arg.Is<Func<InstallState, Task>>(callback => callback != null),
            Arg.Any<CancellationToken>());
        await service.DidNotReceive().PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchGuidance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var service = CreateService(Desktop);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-title").TextContent.Should().Contain("Installer");
            cut.Find("#install-guidance-computer-panel").TextContent.Should().Contain("Ordinateur");
            cut.Find("#install-guidance-ios-steps").TextContent.Should()
                .Contain("Safari").And.Contain("Partager").And.Contain("Sur l’écran d’accueil");
            cut.Find("#install-guidance-android-steps").TextContent.Should()
                .Contain("Installer l’application");
            cut.Find("#install-guidance-samsung-steps").TextContent.Should()
                .Contain("Ajouter la page à");
            cut.Find("#install-guidance-computer-steps").TextContent.Should()
                .Contain("barre d’adresse");
        });
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }
}
