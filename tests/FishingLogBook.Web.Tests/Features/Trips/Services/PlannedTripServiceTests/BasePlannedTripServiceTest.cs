using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.PlannedTripServiceTests;

public class BasePlannedTripServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid GuestUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    protected readonly ITripStore MockTripStore = Substitute.For<ITripStore>();
    protected readonly ITripClient MockTripClient = Substitute.For<ITripClient>();
    protected readonly INetworkService MockNetwork = Substitute.For<INetworkService>();
    protected readonly IActiveTripService MockActiveTrip = Substitute.For<IActiveTripService>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();
    protected readonly PlannedTripService Sut;

    protected BasePlannedTripServiceTest()
    {
        MockTripStore.SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        MockTripStore.GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TripModel>());
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockTripClient.GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PlannedTripOptionDto>());
        MockLogging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        Sut = new PlannedTripService(
            MockTripStore,
            MockTripClient,
            MockNetwork,
            MockActiveTrip,
            MockLogging,
            new FixedTimeProvider(Now));
    }

    protected static TripModel PlannedTrip(
        DateTimeOffset plannedStartOn,
        Guid? ownerUserId = null,
        Guid? tripId = null)
    {
        return new TripModel(
            tripId ?? TripId,
            ownerUserId ?? OwnerUserId,
            TripConstants.Planned,
            plannedStartOn,
            Title: "Corrib",
            SyncStatus: SyncStatus.Synchronised,
            PlannedStartOn: plannedStartOn);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
