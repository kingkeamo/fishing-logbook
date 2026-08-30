using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Profiles.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class BaseTripParticipantServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid InvitedUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly ITripAccessService MockTripAccessService = Substitute.For<ITripAccessService>();
    protected readonly ITripParticipantRepository MockTripParticipantRepository =
        Substitute.For<ITripParticipantRepository>();
    protected readonly ITripRepository MockTripRepository = Substitute.For<ITripRepository>();
    protected readonly IProfileRepository MockProfileRepository = Substitute.For<IProfileRepository>();
    protected readonly IAnglerLookupService MockAnglerLookupService =
        Substitute.For<IAnglerLookupService>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly TripParticipantService Sut;

    protected BaseTripParticipantServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockProfileRepository.UserExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockTripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        MockTripParticipantRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        MockTripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        MockTripParticipantRepository
            .UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripParticipant>(0)));
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(
                new Dictionary<Guid, AnglerSummaryDto>()));
        MockTripAccessService.GivenOwner(Trip(CurrentUserId), CurrentUserId);
        Sut = new TripParticipantService(
            MockTripAccessService,
            MockTripParticipantRepository,
            MockTripRepository,
            MockProfileRepository,
            MockAnglerLookupService,
            MockCurrentUser);
    }

    protected static Trip Trip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            PlaceName = "Lough Corrib"
        };
    }

    protected void GivenParticipantView()
    {
        MockTripAccessService.GivenParticipant(Trip(OtherUserId), CurrentUserId);
    }

    protected void GivenNoAccess()
    {
        MockTripAccessService.GivenNoAccess(TripId);
    }

    protected void GivenExistingMembership(
        Guid userId,
        TripParticipantStatusEnum status,
        DateTimeOffset? removedOn = null)
    {
        MockTripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args => args.TripId == TripId && args.UserId == userId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(Membership(userId, status, removedOn)));
    }

    protected static TripParticipant Membership(
        Guid userId,
        TripParticipantStatusEnum status,
        DateTimeOffset? removedOn = null)
    {
        return new TripParticipant
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            TripId = TripId,
            UserId = userId,
            Status = status,
            InvitedByUserId = CurrentUserId,
            InvitedOn = StartedOn.AddDays(-1),
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : StartedOn.AddHours(-1),
            RemovedOn = removedOn
        };
    }
}
