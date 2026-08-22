using Bunit;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Pages.SystemStatusTests;

public class BaseSystemStatusTest
{
    // MudBlazor registers IAsyncDisposable-only services, so the context must be created
    // per test and disposed with `await using` (never inherit BunitContext on the class).
    protected static BunitContext CreateContext(
        ISystemStatusClient statusClient,
        IAppUpdateService? appUpdate = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(statusClient);
        context.Services.AddSingleton(appUpdate ?? CreateUpdateService(AppUpdateStatus.Current));
        context.Services.AddSingleton(new BuildMetadataConfig
        {
            Version = "0.1.0",
            Sha = "web1234",
            Environment = "prod"
        });
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();

        return context;
    }

    protected static IAppUpdateService CreateUpdateService(AppUpdateStatus status)
    {
        var service = Substitute.For<IAppUpdateService>();
        service.Status.Returns(status);
        return service;
    }
}
