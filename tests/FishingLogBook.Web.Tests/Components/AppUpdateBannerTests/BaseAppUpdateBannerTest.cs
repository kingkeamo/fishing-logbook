using Bunit;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Browser.Update.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Components.AppUpdateBannerTests;

public class BaseAppUpdateBannerTest
{
    protected static IAppUpdateService CreateService(AppUpdateStatus status)
    {
        var service = Substitute.For<IAppUpdateService>();
        service.Status.Returns(status);
        return service;
    }

    protected static BunitContext CreateContext(IAppUpdateService service)
    {
        var context = CreateContext();
        context.Services.AddSingleton(service);
        return context;
    }

    protected static BunitContext CreateBrowserContext(FakeAppUpdateJsRuntime jsRuntime)
    {
        var context = CreateContext();
        context.Services.AddSingleton<IJSRuntime>(jsRuntime);
        context.Services.AddSingleton<IAppUpdateService>(
            new AppUpdateService(jsRuntime, Substitute.For<FishingLogBook.Web.Features.Diagnostics.Services.ILoggingService>()));
        return context;
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
