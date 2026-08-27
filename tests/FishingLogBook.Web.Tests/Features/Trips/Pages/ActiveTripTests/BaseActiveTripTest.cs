using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
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

    protected static BunitContext CreateContext(
        ITripStore store,
        IActiveTripService? activeTrip = null,
        ILocalCatchOwnerService? owner = null,
        IOfflineOwnerContextService? offlineOwner = null,
        ILoggingService? logging = null)
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
        context.Services.AddSingleton<ITripDisplayService>(provider =>
            new TripDisplayService(provider.GetRequiredService<ITimeService>()));
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
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
