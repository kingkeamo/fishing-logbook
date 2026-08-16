using Bunit;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Services;
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
        IDiagnosticLogger? diagnostics = null,
        ILocationService? location = null,
        IDiagnosticSynchroniser? diagnosticSynchroniser = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(synchroniser);
        context.Services.AddSingleton(photoStore);
        context.Services.AddSingleton(diagnostics ?? Substitute.For<IDiagnosticLogger>());
        context.Services.AddSingleton(Substitute.For<ILoggingService>());
        context.Services.AddSingleton(diagnosticSynchroniser ?? Substitute.For<IDiagnosticSynchroniser>());
        context.Services.AddSingleton(new CorrelationContext());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddSingleton(location ?? DeniedLocation());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();

        return context;
    }

    protected static Task Hang()
    {
        return new TaskCompletionSource().Task;
    }

    protected static Task<T> Hang<T>()
    {
        return new TaskCompletionSource<T>().Task;
    }

    protected static ILocationService HangingLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Hang<LocationPromptStatus>());
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Hang<TestCatchLocation?>());
        return location;
    }

    protected static IDiagnosticLogger HangingDiagnostics()
    {
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        diagnostics.LogAsync(
                Arg.Any<FishingLogBook.Shared.Diagnostics.DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Hang());
        return diagnostics;
    }

    protected static ILocationService DeniedLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, true, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((TestCatchLocation?)null);
        return location;
    }

    protected static ILocationService GrantedLocation(TestCatchLocation captured)
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(false, Arg.Any<CancellationToken>())
            .Returns(captured);
        return location;
    }
}
