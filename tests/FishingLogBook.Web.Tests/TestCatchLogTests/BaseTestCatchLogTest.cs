using Bunit;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Offline;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class BaseTestCatchLogTest
{
    protected static BunitContext CreateContext(ITestCatchStore store)
    {
        return CreateContext(store, Substitute.For<ITestCatchSynchroniser>(), Substitute.For<ITestCatchPhotoStore>());
    }

    protected static BunitContext CreateContext(ITestCatchStore store, ITestCatchSynchroniser synchroniser)
    {
        return CreateContext(store, synchroniser, Substitute.For<ITestCatchPhotoStore>());
    }

    protected static BunitContext CreateContext(
        ITestCatchStore store,
        ITestCatchSynchroniser synchroniser,
        ITestCatchPhotoStore photoStore,
        IDiagnosticLogger? diagnostics = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(synchroniser);
        context.Services.AddSingleton(photoStore);
        context.Services.AddSingleton(diagnostics ?? Substitute.For<IDiagnosticLogger>());
        context.Services.AddSingleton(Substitute.For<IDiagnosticSynchroniser>());
        context.Services.AddSingleton(new CorrelationContext());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();

        return context;
    }
}
