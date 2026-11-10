using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Features.OfflineAccess.Components;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Components.OfflineAccessSetupTests;

public class WhenTestingStatuses
{
    private static BunitContext CreateContext(IOfflineAccessService service, IInstallService? install = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        if (install is not null)
        {
            context.Services.AddSingleton(install);
        }

        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    private static IOfflineAccessService ServiceReporting(bool accountEnabled, string deviceState)
    {
        var service = Substitute.For<IOfflineAccessService>();
        service.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessStatusModel(accountEnabled, deviceState));
        return service;
    }

    [Theory]
    [InlineData(false, "ready", "OfflineAccess_Ready", "mud-alert-text-success")]
    [InlineData(false, "unsupported", "OfflineAccess_Unsupported", "mud-alert-text-warning")]
    [InlineData(false, "install-required", "OfflineAccess_InstallRequired", "mud-alert-text-warning")]
    [InlineData(false, "repair", "OfflineAccess_Repair", "mud-alert-text-warning")]
    [InlineData(false, "failed", "OfflineAccess_Failed", "mud-alert-text-warning")]
    [InlineData(false, "cancelled", "OfflineAccess_Cancelled", "mud-alert-text-info")]
    [InlineData(true, "not-configured", "OfflineAccess_EnabledOtherDevice", "mud-alert-text-info")]
    [InlineData(false, "not-configured", "OfflineAccess_NotConfigured", "mud-alert-text-info")]
    public async Task ItShouldDescribeEachDeviceStateWithTheMatchingSeverity(
        bool accountEnabled,
        string deviceState,
        string resourceKey,
        string severityClass)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = ServiceReporting(accountEnabled, deviceState);
        await using var context = CreateContext(service);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()[resourceKey].Value;

        // Act
        var cut = context.Render<OfflineAccessSetup>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-access-status").TextContent.Should().Contain(expected));
        cut.Find("#offline-access-status").ClassList.Should().Contain(severityClass);
        await service.Received(1).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotReadyAndWarnWhenTheStatusCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = Substitute.For<IOfflineAccessService>();
        service.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns<Task<OfflineAccessStatusModel>>(_ => throw new InvalidOperationException("storage"));
        var readiness = new List<bool>();
        await using var context = CreateContext(service);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()["OfflineAccess_Failed"].Value;

        // Act
        var cut = context.Render<OfflineAccessSetup>(parameters => parameters.Add(
            component => component.ReadyChanged,
            Microsoft.AspNetCore.Components.EventCallback.Factory.Create<bool>(new object(), readiness.Add)));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-access-status").TextContent.Should().Contain(expected));
        readiness.Should().Equal(false);
    }

    [Fact]
    public async Task ItShouldRequireInstallationOnAnUninstalledIosPwaWithoutAskingTheService()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = ServiceReporting(false, "not-configured");
        var install = Substitute.For<IInstallService>();
        install.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(new InstallState(false, false, InstallPlatformFamilies.Ios, true));
        await using var context = CreateContext(service, install);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()["OfflineAccess_InstallRequired"].Value;

        // Act
        var cut = context.Render<OfflineAccessSetup>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-access-status").TextContent.Should().Contain(expected));
        cut.Find("#offline-access-enable").HasAttribute("disabled").Should().BeTrue();
        await service.DidNotReceive().GetStatusAsync(Arg.Any<CancellationToken>());
        await install.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRequireInstallationForAnInstalledIosPwa()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = ServiceReporting(false, "not-configured");
        var install = Substitute.For<IInstallService>();
        install.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(new InstallState(true, false, InstallPlatformFamilies.Ios, true));
        await using var context = CreateContext(service, install);

        // Act
        var cut = context.Render<OfflineAccessSetup>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-access-enable").Should().NotBeNull());
        cut.Find("#offline-access-enable").HasAttribute("disabled").Should().BeFalse();
        await service.Received(1).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("#offline-access-remove-device")]
    [InlineData("#offline-access-turn-off")]
    public async Task ItShouldShowFailureAndKeepAccountStateWhenARemovalActionThrows(string buttonSelector)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = ServiceReporting(true, "ready");
        service.RemoveFromDeviceAsync(Arg.Any<CancellationToken>())
            .Returns<Task<OfflineAccessStatusModel>>(_ => throw new InvalidOperationException("storage"));
        service.TurnOffAccountAsync(Arg.Any<CancellationToken>())
            .Returns<Task<OfflineAccessStatusModel>>(_ => throw new InvalidOperationException("server"));
        await using var context = CreateContext(service);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()["OfflineAccess_Failed"].Value;
        var cut = context.Render<OfflineAccessSetup>();
        cut.WaitForAssertion(() => cut.Find(buttonSelector));

        // Act
        await cut.Find(buttonSelector).ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-access-status").TextContent.Should().Contain(expected));
        cut.Find("#offline-access-status").ClassList.Should().Contain("mud-alert-text-warning");
    }
}
