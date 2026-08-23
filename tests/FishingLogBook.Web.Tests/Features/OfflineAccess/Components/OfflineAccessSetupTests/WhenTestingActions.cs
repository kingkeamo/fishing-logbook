using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.OfflineAccess.Components;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Components.OfflineAccessSetupTests;

public class WhenTestingActions
{
    [Fact]
    public async Task ItShouldSetupOnlyAfterTheUserExplicitlyChoosesIt()
    {
        var service = Substitute.For<IOfflineAccessService>();
        service.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessStatusModel(false, "not-configured"));
        service.SetupAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessStatusModel(true, "ready"));
        await using var context = CreateContext(service);
        var cut = context.Render<OfflineAccessSetup>();
        cut.WaitForAssertion(() => cut.Find("#offline-access-enable"));
        await service.DidNotReceive().SetupAsync(Arg.Any<CancellationToken>());

        await cut.Find("#offline-access-enable").ClickAsync();

        cut.WaitForAssertion(() => cut.Find("#offline-access-status").TextContent
            .Should().Contain("ready"));
        await service.Received(1).SetupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExposeBothExplicitRemovalActionsWhenReady()
    {
        var service = Substitute.For<IOfflineAccessService>();
        service.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessStatusModel(true, "ready"));
        await using var context = CreateContext(service);

        var cut = context.Render<OfflineAccessSetup>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#offline-access-remove-device").Should().NotBeNull();
            cut.Find("#offline-access-turn-off").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldShowSafeSetupFailureDiagnostics()
    {
        var service = Substitute.For<IOfflineAccessService>();
        service.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessStatusModel(false, "not-configured"));
        service.SetupAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessStatusModel(
                false,
                "failed",
                new OfflineAccessDeviceResultModel("failed", "EntitlementDecrypted", "OperationError", "The operation failed.")));
        await using var context = CreateContext(service);
        var cut = context.Render<OfflineAccessSetup>();
        cut.WaitForAssertion(() => cut.Find("#offline-access-enable"));

        await cut.Find("#offline-access-enable").ClickAsync();

        cut.WaitForAssertion(() => cut.Find("#offline-access-diagnostic").TextContent.Should()
            .Contain("EntitlementDecrypted")
            .And.Contain("OperationError"));
        await service.Received(1).SetupAsync(Arg.Any<CancellationToken>());
    }

    private static BunitContext CreateContext(IOfflineAccessService service)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }
}
