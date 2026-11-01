using FishingLogBook.Mobile.Features.Trips.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripRemovalServiceTests;

public abstract class BaseTripRemovalServiceTest
{
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly ITripCatchService MockTripCatches = Substitute.For<ITripCatchService>();
    protected readonly TripRemovalService Sut;

    protected BaseTripRemovalServiceTest()
    {
        Sut = new TripRemovalService(MockTrips, MockTripCatches);
    }
}
