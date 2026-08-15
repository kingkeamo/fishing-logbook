using Bunit;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DiagnosticsInspectorTests;

public class BaseDiagnosticsInspectorTest
{
    protected static BunitContext CreateContext(
        IDiagnosticEventStore store,
        IDiagnosticSynchroniser? synchroniser = null,
        INetworkStatus? network = null,
        DiagnosticsClientConfig? config = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(synchroniser ?? Substitute.For<IDiagnosticSynchroniser>());
        context.Services.AddSingleton(new DiagnosticStatus());
        context.Services.AddSingleton(config ?? new DiagnosticsClientConfig { MaxQueueSize = 500 });
        context.Services.AddSingleton(network ?? OnlineNetwork());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static INetworkStatus OnlineNetwork()
    {
        var network = Substitute.For<INetworkStatus>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        return network;
    }

    protected static IDiagnosticEventStore CreateStore(params DiagnosticEvent[] events)
    {
        var store = Substitute.For<IDiagnosticEventStore>();
        store.GetCountAsync(Arg.Any<CancellationToken>()).Returns(events.Length);
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DiagnosticEvent>>(events));
        store.GetStorageEstimateAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageEstimate { Quota = 1000, Usage = 10 });
        return store;
    }
}
