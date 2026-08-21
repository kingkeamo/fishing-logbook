using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallGuidanceTests;

public class WhenTestingRender
{
    [Fact]
    public void ItShouldShowIosSafariInstructions()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        using var context = CreateContext(new InstallState(false, false, InstallPlatformFamilies.Ios, true));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-ios-steps").Children.Should().HaveCount(5);
            cut.FindAll("#install-guidance-ios-browser").Should().BeEmpty();
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
        });
    }

    [Fact]
    public void ItShouldTellIosUsersInAnotherBrowserToUseSafari()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        using var context = CreateContext(new InstallState(false, false, InstallPlatformFamilies.Ios, false));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() => cut.Find("#install-guidance-ios-browser").TextContent.Should().Contain("Safari"));
    }

    [Theory]
    [InlineData(InstallPlatformFamilies.Android, "#install-guidance-android-steps")]
    [InlineData(InstallPlatformFamilies.Windows, "#install-guidance-windows-steps")]
    public void ItShouldShowPlatformFallbackInstructionsWhenNoPromptExists(string platform, string selector)
    {
        using var culture = TestCulture.Use(CultureNames.English);
        using var context = CreateContext(new InstallState(false, false, platform, false));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() => cut.Find(selector).Should().NotBeNull());
    }

    [Fact]
    public void ItShouldShowBothAndroidInstallRoutesWhenTheNativePromptExists()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        using var context = CreateContext(new InstallState(false, true, InstallPlatformFamilies.Android, false));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-action").Should().NotBeNull();
            cut.Find("#install-guidance-android-alternative").TextContent
                .Should().Contain("browser menu");
            cut.Find("#install-guidance-android-steps").Children.Should().HaveCount(3);
        });
    }

    [Fact]
    public void ItShouldShowTheUnknownBrowserFallback()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        using var context = CreateContext(new InstallState(false, false, InstallPlatformFamilies.Other, false));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-unsupported").TextContent
                .Should().Contain("without showing a button");
            cut.Find("#install-guidance-other-steps").TextContent
                .Should().Contain("install icon").And.Contain("browser menu");
        });
    }

    [Fact]
    public void ItShouldShowTheAlreadyInstalledStateWithoutInstructions()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        using var context = CreateContext(new InstallState(true, false, InstallPlatformFamilies.Ios, true));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").Should().NotBeNull();
            cut.FindAll("#install-guidance-ios-steps").Should().BeEmpty();
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldUpdateToInstalledAfterTheNativePromptIsAccepted()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        var service = Substitute.For<IInstallService>();
        service.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(new InstallState(false, true, InstallPlatformFamilies.Windows, false));
        service.PromptAsync(Arg.Any<CancellationToken>()).Returns(InstallResult.Accepted);
        await using var context = CreateContext(service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        await cut.Find("#install-guidance-action").ClickAsync();

        cut.WaitForAssertion(() =>
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("Windows"));
        await service.Received(1).PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepGuidanceUsableWhenTheNativePromptIsDismissed()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        var service = Substitute.For<IInstallService>();
        var state = new InstallState(false, true, InstallPlatformFamilies.Android, false);
        service.GetStateAsync(Arg.Any<CancellationToken>()).Returns(state);
        service.PromptAsync(Arg.Any<CancellationToken>()).Returns(InstallResult.Dismissed);
        await using var context = CreateContext(service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        await cut.Find("#install-guidance-action").ClickAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-dismissed").Should().NotBeNull();
            cut.Find("#install-guidance-action").Should().NotBeNull();
        });
    }

    [Fact]
    public void ItShouldShowFrenchGuidance()
    {
        using var culture = TestCulture.Use(CultureNames.French);
        using var context = CreateContext(new InstallState(false, false, InstallPlatformFamilies.Windows, false));

        var cut = context.Render<InstallGuidance>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-title").TextContent.Should().Contain("Installer");
            cut.Find("#install-guidance-windows-heading").TextContent.Should().Contain("Windows");
        });
    }

    private static BunitContext CreateContext(InstallState state)
    {
        var service = Substitute.For<IInstallService>();
        service.GetStateAsync(Arg.Any<CancellationToken>()).Returns(state);
        return CreateContext(service);
    }

    private static BunitContext CreateContext(IInstallService service)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(service);
        return context;
    }
}
