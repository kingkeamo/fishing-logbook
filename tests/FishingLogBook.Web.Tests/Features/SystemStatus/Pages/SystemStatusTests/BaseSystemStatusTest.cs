using Bunit;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Pages.SystemStatusTests;

public class BaseSystemStatusTest
{
    // MudBlazor registers IAsyncDisposable-only services, so the context must be created
    // per test and disposed with `await using` (never inherit BunitContext on the class).
    protected static BunitContext CreateContext(ISystemStatusClient statusClient)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(statusClient);
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();

        return context;
    }
}
