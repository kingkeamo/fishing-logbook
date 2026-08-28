using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class BaseActiveTripTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected static Task<ITripStore> StoreWithActiveTripAsync()
    {
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip());
        return Task.FromResult(store);
    }

    protected static ICatchStore QuietCatchStore(params CatchModel[] catches)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(catches);
        return store;
    }

    protected static CatchModel CatchFor(Guid? tripId)
    {
        var catchId = Guid.NewGuid();
        return new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-26T09:48:00Z"),
            [],
            TripId: tripId);
    }

    protected static BunitContext CreateContext(
        ITripStore store,
        IActiveTripService? activeTrip = null,
        ILocalCatchOwnerService? owner = null,
        IOfflineOwnerContextService? offlineOwner = null,
        ILoggingService? logging = null,
        ICatchStore? catchStore = null,
        IAnglerPreferencesProvider? anglerPreferences = null,
        ITripClient? tripClient = null,
        IModalService? modalService = null,
        ITripNoteWriteService? noteWriter = null,
        INetworkService? network = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(activeTrip ?? QuietActiveTripService());
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(offlineOwner ?? UnlockedOfflineOwner());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton<ITimeService>(TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(Substitute.For<ITripPhotographStore>());
        context.Services.AddSingleton(Substitute.For<ITripNoteStore>());
        context.Services.AddSingleton(catchStore ?? QuietCatchStore());
        context.Services.AddSingleton(tripClient ?? Substitute.For<ITripClient>());
        context.Services.AddSingleton(Substitute.For<IPhotographPreparationService>());
        context.Services.AddSingleton<ITripDisplayService>(provider =>
            new TripDisplayService(provider.GetRequiredService<ITimeService>()));
        context.Services.AddSingleton(anglerPreferences ?? QuietAnglerPreferences());
        context.Services.AddSingleton<ITripTimelineService>(new TripTimelineService());
        context.Services.AddSingleton<IMeasurementService>(new MeasurementService());
        context.Services.AddSingleton(modalService ?? ConfirmingModalService());
        context.Services.AddSingleton(noteWriter ?? Substitute.For<ITripNoteWriteService>());
        context.Services.AddSingleton(network ?? OnlineNetwork());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static INetworkService OnlineNetwork(bool isOnline = true)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(isOnline);
        return network;
    }

    protected static IAnglerPreferencesProvider QuietAnglerPreferences(
        params FishingLocationPreferenceDto[] locations)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(AnglerPreferencesModel.Empty with { Locations = locations });
        return provider;
    }

    protected static IModalService ConfirmingModalService(bool confirm = true)
    {
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(confirm);
        return modalService;
    }

    protected static IActiveTripService QuietActiveTripService()
    {
        var service = Substitute.For<IActiveTripService>();
        service.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        service.TryAttachLocationAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        service.FinishAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns(call => Finished(call.ArgAt<TripModel>(0)));
        return service;
    }

    protected static ILocalCatchOwnerService SignedInOwner(Guid? userId = null)
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(userId ?? OwnerUserId);
        return owner;
    }

    protected static IOfflineOwnerContextService UnlockedOfflineOwner(Guid? userId = null)
    {
        var offlineOwner = Substitute.For<IOfflineOwnerContextService>();
        offlineOwner.IsUnlocked.Returns(true);
        offlineOwner.Owner.Returns(new OfflineOwnerModel(userId ?? OwnerUserId, 1));
        return offlineOwner;
    }

    protected static IOfflineOwnerContextService LockedOfflineOwner()
    {
        var offlineOwner = Substitute.For<IOfflineOwnerContextService>();
        offlineOwner.IsUnlocked.Returns(false);
        offlineOwner.Owner.Returns((OfflineOwnerModel?)null);
        return offlineOwner;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static TripModel StoredActiveTrip(
        string? title = null,
        string? placeName = null,
        DateTimeOffset? startedOn = null)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            startedOn ?? StartedOn,
            Title: title,
            PlaceName: placeName);
    }

    protected static TripModel StoredCompletedTrip(DateTimeOffset? endedOn = null)
    {
        return Finished(StoredActiveTrip(), endedOn);
    }

    private static TripModel Finished(TripModel trip, DateTimeOffset? endedOn = null)
    {
        return trip with
        {
            Status = TripConstants.Completed,
            EndedOn = endedOn ?? trip.StartedOn.AddHours(6).AddMinutes(43)
        };
    }
}
