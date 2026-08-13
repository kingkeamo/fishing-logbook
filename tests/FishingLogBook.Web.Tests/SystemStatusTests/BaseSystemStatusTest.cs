using Bunit;
using FishingLogBook.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.SystemStatusTests;

public class BaseSystemStatusTest
{
    // MudBlazor registers IAsyncDisposable-only services, so the context must be created
    // per test and disposed with `await using` (never inherit BunitContext on the class).
    protected static BunitContext CreateContext(ISystemStatusClient statusClient)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(statusClient);

        return context;
    }
}
