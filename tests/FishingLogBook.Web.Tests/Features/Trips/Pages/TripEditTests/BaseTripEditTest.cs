using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
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

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;

public class BaseTripEditTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T13:02:00Z");

    protected static BunitContext CreateContext(
        ITripStore store,
        ICatchStore? catchStore = null,
        ILocalCatchOwnerService? owner = null,
        IOfflineOwnerContextService? offlineOwner = null,
        IActiveTripService? activeTrip = null,
        IModalService? modalService = null,
        ILoggingService? logging = null,
        IAnglerPreferencesProvider? anglerPreferences = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(catchStore ?? QuietCatchStore());
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(offlineOwner ?? UnlockedOfflineOwner());
        context.Services.AddSingleton(activeTrip ?? Substitute.For<IActiveTripService>());
        context.Services.AddSingleton(modalService ?? ConfirmingModalService());
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddSingleton<ITripDisplayService>(provider =>
            new TripDisplayService(provider.GetRequiredService<ITimeService>()));
        context.Services.AddSingleton<ITimeService>(TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(anglerPreferences ?? QuietAnglerPreferences());
        context.Services.AddSingleton<IMeasurementService>(new MeasurementService());
        context.Services.AddSingleton(Substitute.For<ITripPhotographStore>());
        context.Services.AddSingleton(Substitute.For<ITripNoteStore>());
        context.Services.AddSingleton(Substitute.For<ITripNoteWriteService>());
        context.Services.AddSingleton(Substitute.For<ITripClient>());
        context.Services.AddSingleton(Substitute.For<IPhotographPreparationService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ICatchStore QuietCatchStore(params CatchModel[] catches)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(catches);
        return store;
    }

    protected static CatchModel CatchFor(Guid? tripId, string? speciesName = "Brown Trout")
    {
        return new CatchModel(
            Guid.NewGuid(),
            StartedOn.AddMinutes(15),
            [],
            speciesName,
            TripId: tripId);
    }

    protected static IModalService ConfirmingModalService(bool confirm = true)
    {
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(confirm);
        return modalService;
    }

    protected static IAnglerPreferencesProvider QuietAnglerPreferences()
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty);
        return provider;
    }

    protected static ILocalCatchOwnerService SignedInOwner()
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        return owner;
    }

    protected static IOfflineOwnerContextService UnlockedOfflineOwner()
    {
        var offlineOwner = Substitute.For<IOfflineOwnerContextService>();
        offlineOwner.IsUnlocked.Returns(true);
        offlineOwner.Owner.Returns(new OfflineOwnerModel(OwnerUserId, 1));
        return offlineOwner;
    }

    protected static ITripStore StoreWithTrip(TripModel? trip)
    {
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(trip);
        return store;
    }

    protected static TripModel ActiveTrip(string? title = null, string? placeName = null)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            Title: title,
            PlaceName: placeName);
    }
}
