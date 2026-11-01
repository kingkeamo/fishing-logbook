using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Queries;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetPlannedTripsQueryTests;

public class BaseGetPlannedTripsQueryTest
{
    protected static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected readonly IPlannedTripService MockPlannedTripService = Substitute.For<IPlannedTripService>();
    protected readonly GetPlannedTripsHandler Sut;

    protected BaseGetPlannedTripsQueryTest()
    {
        Sut = new GetPlannedTripsHandler(MockPlannedTripService, TestMapper.Create());
    }
}
