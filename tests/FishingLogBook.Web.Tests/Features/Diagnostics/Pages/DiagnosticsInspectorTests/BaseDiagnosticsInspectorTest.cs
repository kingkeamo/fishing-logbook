using Bunit;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

public class BaseDiagnosticsInspectorTest
{
    protected static BunitContext CreateContext(
        IDiagnosticEventStore store,
        IDiagnosticSynchroniser? synchroniser = null,
        INetworkService? network = null,
        DiagnosticsClientConfig? config = null,
        ILoggingService? logging = null,
        IDiagnosticIndexedDbProbe? probe = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(synchroniser ?? Substitute.For<IDiagnosticSynchroniser>());
        context.Services.AddSingleton(new DiagnosticStatusModel());
        context.Services.AddSingleton(config ?? new DiagnosticsClientConfig { MaxQueueSize = 500 });
        context.Services.AddSingleton(network ?? OnlineNetwork());
        context.Services.AddSingleton(logging ?? SilentLogging());
        context.Services.AddSingleton(probe ?? SilentProbe());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static INetworkService OnlineNetwork()
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        return network;
    }

    protected static IDiagnosticEventStore CreateStore(params DiagnosticEventModel[] events)
    {
        var store = Substitute.For<IDiagnosticEventStore>();
        store.GetCountAsync(Arg.Any<CancellationToken>()).Returns(events.Length);
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DiagnosticEventModel>>(events));
        store.GetStorageEstimateAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageEstimate { Quota = 1000, Usage = 10 });
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspectionModel
            {
                Exists = true,
                HasStore = true,
                Count = events.Length
            });
        return store;
    }

    protected static ILoggingService SilentLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.GetLastErrorAsync(Arg.Any<CancellationToken>()).Returns((LastErrorLog?)null);
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static IDiagnosticIndexedDbProbe SilentProbe()
    {
        var probe = Substitute.For<IDiagnosticIndexedDbProbe>();
        probe.RunIsolatedAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticProbeResultModel
            {
                DatabaseName = DiagnosticIndexedDbProbe.IsolatedDatabaseName,
                LastCompletedStage = DiagnosticIndexedDbProbe.StageCountReturned,
                Count = 0
            });
        return probe;
    }
}
