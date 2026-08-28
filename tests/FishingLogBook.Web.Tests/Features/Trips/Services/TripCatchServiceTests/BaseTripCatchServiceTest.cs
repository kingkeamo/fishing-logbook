using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripCatchServiceTests;

public class BaseTripCatchServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OtherTripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab");
    protected static readonly Guid PikeCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid TroutCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset EndedOn = DateTimeOffset.Parse("2026-08-17T16:00:00Z");

    protected readonly ICatchStore MockCatchStore = Substitute.For<ICatchStore>();
    protected readonly ICatchClient MockCatchClient = Substitute.For<ICatchClient>();
    protected readonly ITripClient MockTripClient = Substitute.For<ITripClient>();
    protected readonly TripCatchService Sut;

    protected BaseTripCatchServiceTest()
    {
        MockCatchStore.GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        MockCatchStore.UpdateTripAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        MockCatchClient.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        MockTripClient.AssociateCatchesAsync(
                Arg.Any<Guid>(),
                Arg.Any<AssociateTripCatchesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new TripCatchAssociationDto(
                call.ArgAt<AssociateTripCatchesDto>(1).CatchIds,
                []));
        Sut = new TripCatchService(MockCatchStore, MockCatchClient, MockTripClient);
    }

    protected static TripCatchScopeModel CompletedScope()
    {
        return new TripCatchScopeModel(TripId, OwnerUserId, StartedOn, EndedOn);
    }

    protected static TripCatchScopeModel ActiveScope(DateTimeOffset? startedOn = null)
    {
        return new TripCatchScopeModel(TripId, OwnerUserId, startedOn ?? StartedOn);
    }

    protected static CatchModel Catch(
        Guid catchId,
        DateTimeOffset caughtOn,
        Guid? tripId = null,
        Guid? userId = null)
    {
        return new CatchModel(
            catchId,
            caughtOn,
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)],
            "Pike",
            UserId: userId ?? OwnerUserId,
            TripId: tripId);
    }

    protected static CatchViewDto RemoteCatch(
        Guid catchId,
        DateTimeOffset caughtOn,
        Guid? tripId = null,
        Guid? userId = null)
    {
        return new CatchViewDto(catchId, userId ?? OwnerUserId, caughtOn)
        {
            TripId = tripId,
            SpeciesName = "Pike"
        };
    }
}
