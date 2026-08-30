using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripAccessServiceTests;

public class BaseTripAccessServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OwnerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly ITripRepository MockTripRepository = Substitute.For<ITripRepository>();
    protected readonly ITripParticipantRepository MockTripParticipantRepository =
        Substitute.For<ITripParticipantRepository>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly TripAccessService Sut;

    protected BaseTripAccessServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockTripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        Sut = new TripAccessService(
            MockTripRepository,
            MockTripParticipantRepository,
            MockCurrentUser);
    }

    protected void GivenTrip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = TripId,
                OwnerUserId = ownerUserId,
                Status = status,
                StartedOn = StartedOn
            }));
    }

    protected void GivenNoTrip()
    {
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
    }

    protected void GivenParticipant(
        TripParticipantStatusEnum status = TripParticipantStatusEnum.Accepted,
        DateTimeOffset? removedOn = null)
    {
        MockTripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args =>
                    args.TripId == TripId && args.UserId == CurrentUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = TripId,
                UserId = CurrentUserId,
                Status = status,
                InvitedByUserId = OwnerUserId,
                InvitedOn = StartedOn.AddDays(-1),
                RespondedOn = status == TripParticipantStatusEnum.Pending
                    ? null
                    : StartedOn.AddHours(-1),
                RemovedOn = removedOn
            }));
    }
}
