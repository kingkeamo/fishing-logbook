using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.AssociateTripCatchesCommandTests;

public class BaseAssociateTripCatchesCommandTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid CatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    protected readonly ITripCatchService MockTripCatchService = Substitute.For<ITripCatchService>();
    protected readonly AssociateTripCatchesHandler Sut;

    protected BaseAssociateTripCatchesCommandTest()
    {
        Sut = new AssociateTripCatchesHandler(MockTripCatchService, TestMapper.Create());
    }

    protected static AssociateTripCatchesCommand Command(params Guid[] catchIds)
    {
        return new AssociateTripCatchesCommand
        {
            TripId = TripId,
            CatchIds = catchIds.Length == 0 ? [CatchId] : catchIds
        };
    }
}
