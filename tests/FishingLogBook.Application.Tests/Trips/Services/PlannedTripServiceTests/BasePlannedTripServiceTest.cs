using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.PlannedTripServiceTests;

public class BasePlannedTripServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OrganiserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid GuestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly Guid DeclinedId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-31T23:30:00Z");

    protected readonly ITripRepository MockTripRepository = Substitute.For<ITripRepository>();
    protected readonly ITripParticipantRepository MockTripParticipantRepository =
        Substitute.For<ITripParticipantRepository>();
    protected readonly IAnglerLookupService MockAnglerLookupService = Substitute.For<IAnglerLookupService>();
    protected readonly TimeProvider MockTimeProvider = Substitute.For<TimeProvider>();
    protected readonly PlannedTripService Sut;

    protected BasePlannedTripServiceTest()
    {
        MockTimeProvider.GetUtcNow().Returns(Now);
        GivenPlannedTrips();
        MockTripParticipantRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(new Dictionary<Guid, AnglerSummaryDto>()));
        Sut = new PlannedTripService(
            MockTripRepository,
            MockTripParticipantRepository,
            MockAnglerLookupService,
            MockTimeProvider);
    }

    protected static Trip PlannedTrip(
        DateTimeOffset plannedStartOn,
        Guid? ownerUserId = null,
        Guid? tripId = null,
        TripStatusEnum status = TripStatusEnum.Planned,
        string? title = null)
    {
        return new Trip
        {
            Id = tripId ?? Guid.NewGuid(),
            OwnerUserId = ownerUserId ?? CurrentUserId,
            Title = title,
            PlaceName = "Lough Corrib",
            Status = status,
            StartedOn = plannedStartOn,
            PlannedStartOn = plannedStartOn
        };
    }

    protected static TripParticipant Participant(
        Guid tripId,
        Guid userId,
        TripParticipantStatusEnum status,
        DateTimeOffset? removedOn = null,
        int invitedMinutesAfterNow = 0)
    {
        return new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = userId,
            Status = status,
            InvitedByUserId = OrganiserId,
            InvitedOn = Now.AddMinutes(invitedMinutesAfterNow),
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : Now.AddMinutes(invitedMinutesAfterNow + 1),
            RemovedOn = removedOn
        };
    }

    protected void GivenPlannedTrips(params Trip[] trips)
    {
        MockTripRepository
            .FindPlannedAsync(Arg.Any<FindPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>(trips));
    }

    protected void GivenParticipants(Guid tripId, params TripParticipant[] participants)
    {
        MockTripParticipantRepository
            .GetByTripIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(participants));
    }

    protected void GivenNames(params (Guid UserId, string Name)[] anglers)
    {
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(
                anglers.ToDictionary(
                    angler => angler.UserId,
                    angler => new AnglerSummaryDto(angler.UserId, angler.Name, null, null, null))));
    }
}
