using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Browser.Install.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallGuidanceTests;

public class BaseInstallGuidanceTest
{
    protected static readonly InstallState IosSafari =
        new(false, false, InstallPlatformFamilies.Ios, true);

    protected static readonly InstallState Android =
        new(false, false, InstallPlatformFamilies.Android, false);

    protected static readonly InstallState Desktop =
        new(false, false, InstallPlatformFamilies.Desktop, false);

    protected static IInstallService CreateService(
        InstallState state,
        InstallResult promptResult = InstallResult.Unavailable)
    {
        var service = Substitute.For<IInstallService>();
        service.GetStateAsync(Arg.Any<CancellationToken>()).Returns(state);
        service.PromptAsync(Arg.Any<CancellationToken>()).Returns(promptResult);
        service.SubscribeAsync(Arg.Any<Func<InstallState, Task>>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAsyncDisposable>());
        return service;
    }

    protected static BunitContext CreateContext(
        IInstallService service,
        ILoggingService? logging = null)
    {
        var context = CreateContext();
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        return context;
    }

    protected static BunitContext CreateBrowserContext(FakeInstallJsRuntime jsRuntime)
    {
        var context = CreateContext();
        context.Services.AddSingleton<IJSRuntime>(jsRuntime);
        context.Services.AddSingleton<IInstallService>(new InstallService(jsRuntime));
        context.Services.AddSingleton(Substitute.For<ILoggingService>());
        return context;
    }

    protected static bool IsPanelExpanded(IRenderedComponent<InstallGuidance> cut, string panelId)
    {
        return cut.Find($"#{panelId}").ClassList.Contains("mud-panel-expanded");
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }
}
