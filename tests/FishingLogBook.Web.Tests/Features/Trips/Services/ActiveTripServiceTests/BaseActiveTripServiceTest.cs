using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.ActiveTripServiceTests;

public class BaseActiveTripServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly ITripStore MockTripStore = Substitute.For<ITripStore>();
    protected readonly ILocationService MockLocationService = Substitute.For<ILocationService>();
    protected readonly IAnglerPreferencesProvider MockAnglerPreferences =
        Substitute.For<IAnglerPreferencesProvider>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();
    protected readonly ActiveTripService Sut;

    protected BaseActiveTripServiceTest()
    {
        MockTripStore.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        MockTripStore.SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        MockLogging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        MockAnglerPreferences.GetAsync(Arg.Any<CancellationToken>())
            .Returns(AnglerPreferencesModel.Empty);
        Sut = new ActiveTripService(
            MockTripStore,
            MockLocationService,
            MockAnglerPreferences,
            MockLogging);
    }

    protected static TripModel ActiveTrip(Guid? tripId = null, TripLocationModel? location = null)
    {
        return new TripModel(
            tripId ?? TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            Location: location);
    }

    protected static CatchLocationModel CapturedLocation()
    {
        return new CatchLocationModel(
            53.4419,
            -9.2531,
            8,
            StartedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }
}
