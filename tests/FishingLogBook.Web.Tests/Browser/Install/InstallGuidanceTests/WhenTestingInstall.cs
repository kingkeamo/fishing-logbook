using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallGuidanceTests;

public class WhenTestingInstall : BaseInstallGuidanceTest
{
    private static readonly InstallState AndroidPromptable =
        new(false, true, InstallPlatformFamilies.Android, false);

    [Fact]
    public async Task ItShouldFallBackToManualGuidanceWhenTheNativePromptFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var service = CreateService(AndroidPromptable);
        service.PromptAsync(Arg.Any<CancellationToken>())
            .Returns<InstallResult>(_ => throw new InvalidOperationException("prompt failed"));
        await using var context = CreateContext(service, logging);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        // Act
        await cut.Find("#install-guidance-action").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.Find("#install-guidance-android-steps").Children.Should().HaveCount(4);
            cut.Find("#install-guidance-ios-steps").Children.Should().HaveCount(5);
            cut.Find("#install-guidance-computer-steps").Children.Should().HaveCount(4);
        });
        await service.Received(1).PromptAsync(Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "install detection",
            Arg.Is<Exception>(exception => exception.Message == "prompt failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepManualGuidanceWhenTheNativePromptIsDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AndroidPromptable, InstallResult.Dismissed);
        await using var context = CreateContext(service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        // Act
        await cut.Find("#install-guidance-action").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-dismissed").TextContent.Should().Contain("not installed");
            cut.Find("#install-guidance-action").Should().NotBeNull();
            cut.Find("#install-guidance-android-steps").Children.Should().HaveCount(4);
            cut.Find("#install-guidance-ios-steps").Children.Should().HaveCount(5);
        });
        await service.Received(1).PromptAsync(Arg.Any<CancellationToken>());
        await service.Received(2).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheInstalledStateAfterTheNativePromptIsAccepted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AndroidPromptable, InstallResult.Accepted);
        await using var context = CreateContext(service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        // Act
        await cut.Find("#install-guidance-action").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("App is installed");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.Find("#install-guidance-android-steps").Should().NotBeNull();
        });
        await service.Received(1).PromptAsync(Arg.Any<CancellationToken>());
        await service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }
}
