using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Stores;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchStoreTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

/// <summary>Runs the real trip service against real SQLite so persistence and outbox state are authoritative.</summary>
public abstract class BaseTripServiceTest : BaseSqliteCatchStoreTest
{
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected readonly ITripClient MockClient = Substitute.For<ITripClient>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly TimeProvider Clock = new FixedClock(Now);
    protected readonly SqliteTripStore TripStore;
    protected readonly TripService TripSut;

    protected BaseTripServiceTest()
    {
        MockSessionCoordinator.TryCommitAsync(
                Arg.Any<FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await call.Arg<Func<CancellationToken, Task>>()(call.ArgAt<CancellationToken>(2));
                return true;
            });
        MockConnectivity.IsOnline.Returns(true);
        TripStore = CreateTripStore();
        TripSut = new TripService(TripStore, MockClient, MockSessionCoordinator, MockConnectivity, Clock);
    }

    protected SqliteTripStore CreateTripStore()
    {
        return new SqliteTripStore(ConnectionFactory, Database, MockSessionCoordinator, OutboxStore, MockNotifier, Clock);
    }

    protected static TripDetailDto ServerTrip(Guid id, string status = TripConstants.Active, string role = TripParticipantConstants.Owner,
        Guid? owner = null)
    {
        return new TripDetailDto(new TripViewDto(id, owner ?? OwnerUserId, status, Now.AddHours(-2))
        {
            Title = "Server trip"
        })
        { Role = role };
    }

    protected static TripSummaryDto Summary(TripDetailDto detail)
    {
        return new TripSummaryDto(detail.Trip.Id, detail.Trip.Status, detail.Trip.StartedOn)
        {
            OwnerUserId = detail.Trip.OwnerUserId,
            Role = detail.Role,
            Title = detail.Trip.Title
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
