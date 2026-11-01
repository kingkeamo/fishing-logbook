using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.PlannedTripEditorTests;

public class BasePlannedTripEditorTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    protected readonly ITripStore TripStore = Substitute.For<ITripStore>();
    protected readonly IPlannedTripService PlannedTrips = Substitute.For<IPlannedTripService>();
    protected readonly ITripSynchroniser Synchroniser = Substitute.For<ITripSynchroniser>();
    protected readonly INetworkService Network = Substitute.For<INetworkService>();
    protected readonly IModalService Modals = Substitute.For<IModalService>();
    protected readonly ILoggingService Logging = Substitute.For<ILoggingService>();

    protected BasePlannedTripEditorTest()
    {
        Network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        Synchroniser.SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        Modals.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(true);
        Logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    protected BunitContext CreateContext()
    {
        var preferences = Substitute.For<IAnglerPreferencesProvider>();
        preferences.GetAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty);
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(TripStore);
        context.Services.AddSingleton(PlannedTrips);
        context.Services.AddSingleton(Synchroniser);
        context.Services.AddSingleton(Network);
        context.Services.AddSingleton(Modals);
        context.Services.AddSingleton(Logging);
        context.Services.AddSingleton(preferences);
        context.Services.AddSingleton<ITimeService>(TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static TripModel Planned(
        string status = TripConstants.Planned,
        Guid? ownerUserId = null,
        SyncStatus syncStatus = SyncStatus.Synchronised)
    {
        return new TripModel(
            TripId,
            ownerUserId ?? OwnerUserId,
            status,
            PlannedStart,
            Title: "Corrib",
            PlaceName: "Shore",
            SyncStatus: syncStatus,
            PlannedStartOn: PlannedStart);
    }
}
