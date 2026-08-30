using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Repositories;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripCatchServiceTests;

public class BaseTripCatchServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid CatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid OtherCatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2026-08-17T09:12:00Z");

    protected readonly ITripAccessService MockTripAccessService = Substitute.For<ITripAccessService>();
    protected readonly ICatchRepository MockCatchRepository = Substitute.For<ICatchRepository>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly TripCatchService Sut;

    protected BaseTripCatchServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockCatchRepository.AssociateTripAsync(Arg.Any<PersistCatchTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        Sut = new TripCatchService(MockTripAccessService, MockCatchRepository, MockCurrentUser);
    }

    protected void GivenTrip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        MockTripAccessService.GivenOwner(BuildTrip(ownerUserId, status), CurrentUserId);
    }

    protected void GivenSharedTrip(TripStatusEnum status = TripStatusEnum.Active)
    {
        MockTripAccessService.GivenParticipant(BuildTrip(OtherUserId, status), CurrentUserId);
    }

    protected void GivenNoTrip()
    {
        MockTripAccessService.GivenNoAccess(TripId);
    }

    protected static Trip BuildTrip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
    }

    protected void GivenCatch(
        Guid catchId,
        Guid userId,
        DateTimeOffset? caughtOn = null,
        Guid? tripId = null)
    {
        MockCatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = catchId,
                UserId = userId,
                AnglerUserId = userId,
                RecordedByUserId = userId,
                CaughtOn = caughtOn ?? CaughtOn,
                TripId = tripId
            }));
    }

    protected void GivenNoCatch(Guid catchId)
    {
        MockCatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
    }
}
