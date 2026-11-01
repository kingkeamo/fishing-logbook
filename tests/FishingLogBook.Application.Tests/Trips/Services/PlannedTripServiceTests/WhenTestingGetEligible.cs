using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.PlannedTripServiceTests;

public class WhenTestingGetEligible : BasePlannedTripServiceTest
{
    [Fact]
    public async Task ItShouldReturnTheRepositoryFailureWithoutLookingUpParticipants()
    {
        // Arrange
        MockTripRepository
            .FindPlannedAsync(Arg.Any<FindPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<Trip>>("Failed to save the trip."));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockTripParticipantRepository.DidNotReceive().GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingAndLookUpNobodyWhenNoTripIsEligible()
    {
        // Arrange
        GivenPlannedTrips();

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await MockTripParticipantRepository.DidNotReceive().GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockAnglerLookupService.DidNotReceive().DescribeAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskForThreeDaysEitherSideOfNowByDefault()
    {
        // Arrange
        // Act
        await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        await MockTripRepository.Received(1).FindPlannedAsync(
            Arg.Is<FindPlannedTripsArgs>(args => args.UserId == CurrentUserId
                && args.From == Now.AddDays(-3)
                && args.To == Now.AddDays(3)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(30, 30)]
    [InlineData(31, 3)]
    [InlineData(-4, 3)]
    public async Task ItShouldUseTheRequestedWindowWhenItIsInRangeAndTheDefaultOtherwise(int requested, int expectedDays)
    {
        // Arrange
        // Act
        await Sut.GetEligibleAsync(
            new GetPlannedTripsArgs { UserId = CurrentUserId, WindowDays = requested },
            CancellationToken.None);

        // Assert
        await MockTripRepository.Received(1).FindPlannedAsync(
            Arg.Is<FindPlannedTripsArgs>(args => args.From == Now.AddDays(-expectedDays) && args.To == Now.AddDays(expectedDays)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTripsOnTheWindowEdgesAndDropAnythingBeyondThem()
    {
        // Arrange
        var atEarliest = PlannedTrip(Now.AddDays(-3));
        var atLatest = PlannedTrip(Now.AddDays(3));
        var tooEarly = PlannedTrip(Now.AddDays(-3).AddTicks(-1));
        var tooLate = PlannedTrip(Now.AddDays(3).AddTicks(1));
        GivenPlannedTrips(tooLate, atLatest, tooEarly, atEarliest);

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.Value.Select(option => option.TripId).Should().Equal(atEarliest.Id, atLatest.Id);
    }

    [Theory]
    [InlineData(TripStatusEnum.Active)]
    [InlineData(TripStatusEnum.Completed)]
    [InlineData(TripStatusEnum.Cancelled)]
    public async Task ItShouldNeverOfferATripThatIsNotPlanned(TripStatusEnum status)
    {
        // Arrange
        GivenPlannedTrips(PlannedTrip(Now.AddHours(2), status: status));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnThePlanWithItsOrganiserAndTheOwnerRole()
    {
        // Arrange
        var trip = PlannedTrip(Now.AddHours(5), title: "Corrib weekend");
        GivenPlannedTrips(trip);
        GivenNames((CurrentUserId, "Me"));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        var option = result.Value.Should().ContainSingle().Subject;
        option.TripId.Should().Be(trip.Id);
        option.Title.Should().Be("Corrib weekend");
        option.PlaceName.Should().Be("Lough Corrib");
        option.PlannedStartOn.Should().Be(Now.AddHours(5));
        option.OrganiserUserId.Should().Be(CurrentUserId);
        option.OrganiserName.Should().Be("Me");
        option.Role.Should().Be(TripParticipantConstants.Owner);
        option.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldMarkATripOrganisedBySomeoneElseAsParticipant()
    {
        // Arrange
        var trip = PlannedTrip(Now.AddHours(5), ownerUserId: OrganiserId);
        GivenPlannedTrips(trip);
        GivenParticipants(trip.Id, Participant(trip.Id, CurrentUserId, TripParticipantStatusEnum.Accepted));
        GivenNames((OrganiserId, "Sam"), (CurrentUserId, "Me"));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        var option = result.Value.Should().ContainSingle().Subject;
        option.Role.Should().Be(TripParticipantConstants.Participant);
        option.OrganiserName.Should().Be("Sam");
    }

    [Fact]
    public async Task ItShouldListAcceptedAndPendingGuestsButNotDeclinedOrRemovedOnes()
    {
        // Arrange
        var trip = PlannedTrip(Now.AddHours(5));
        GivenPlannedTrips(trip);
        GivenParticipants(
            trip.Id,
            Participant(trip.Id, GuestId, TripParticipantStatusEnum.Accepted, invitedMinutesAfterNow: 1),
            Participant(trip.Id, DeclinedId, TripParticipantStatusEnum.Declined, invitedMinutesAfterNow: 2),
            Participant(trip.Id, OrganiserId, TripParticipantStatusEnum.Pending, invitedMinutesAfterNow: 3),
            Participant(trip.Id, Guid.NewGuid(), TripParticipantStatusEnum.Accepted, removedOn: Now, invitedMinutesAfterNow: 4));
        GivenNames((GuestId, "Pat"), (OrganiserId, "Sam"), (CurrentUserId, "Me"));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        var participants = result.Value.Single().Participants;
        participants.Select(participant => (participant.UserId, participant.DisplayName, participant.Status))
            .Should().Equal(
                (GuestId, "Pat", "Accepted"),
                (OrganiserId, "Sam", "Pending"));
    }

    [Fact]
    public async Task ItShouldOrderTripsByPlannedStartThenById()
    {
        // Arrange
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var later = PlannedTrip(Now.AddHours(30));
        var sameStartHigh = PlannedTrip(Now.AddHours(2), tripId: highId);
        var sameStartLow = PlannedTrip(Now.AddHours(2), tripId: lowId);
        GivenPlannedTrips(later, sameStartHigh, sameStartLow);

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.Value.Select(option => option.TripId).Should().Equal(lowId, highId, later.Id);
    }

    [Fact]
    public async Task ItShouldReturnTheParticipantFailure()
    {
        // Arrange
        var trip = PlannedTrip(Now.AddHours(5));
        GivenPlannedTrips(trip);
        MockTripParticipantRepository
            .GetByTripIdAsync(trip.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripParticipant>>("Failed to load participants."));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReturnTheLookupFailure()
    {
        // Arrange
        GivenPlannedTrips(PlannedTrip(Now.AddHours(5)));
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyDictionary<Guid, FishingLogBook.Shared.Dtos.AnglerSummaryDto>>("Lookup failed."));

        // Act
        var result = await Sut.GetEligibleAsync(new GetPlannedTripsArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }
}
