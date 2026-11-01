using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingActivate : BaseTripServiceTest
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.Parse("2026-09-12T06:00:00Z");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    public WhenTestingActivate()
    {
        MockTripRepository.ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Trip>(0)));
    }

    [Fact]
    public async Task ItShouldNotRevealATripToSomeoneWhoIsNotOnIt()
    {
        // Arrange
        MockTripAccessService.GivenNoAccess(TripId);

        // Act
        var result = await Sut.ActivateAsync(new ActivateTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripRepository.DidNotReceive().ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateAPlannedTripForItsOrganiserAtTheCurrentTime()
    {
        // Arrange
        var planned = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripAccessService.GivenOwner(planned, CurrentUserId);

        // Act
        var result = await Sut.ActivateAsync(new ActivateTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripConstants.Active);
        result.Value.StartedOn.Should().Be(Now);
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn);
        await MockTripRepository.Received(1).ActivateAsync(
            Arg.Is<Trip>(trip => trip.Id == TripId
                && trip.Status == TripStatusEnum.Active
                && trip.StartedOn == Now
                && trip.PlannedStartOn == PlannedStartOn
                && trip.EndedOn == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateAPlannedTripForAnAcceptedParticipant()
    {
        // Arrange
        var planned = StoredTrip(ownerUserId: OtherUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripAccessService.GivenParticipant(planned, CurrentUserId);

        // Act
        var result = await Sut.ActivateAsync(new ActivateTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerUserId.Should().Be(OtherUserId);
        await MockTripRepository.Received(1).ActivateAsync(
            Arg.Is<Trip>(trip => trip.OwnerUserId == OtherUserId && trip.Status == TripStatusEnum.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAnswerARepeatedActivationWithoutRecordingAnotherStart()
    {
        // Arrange
        var activated = StoredTrip(plannedStartOn: PlannedStartOn);
        MockTripAccessService.GivenOwner(activated, CurrentUserId);

        // Act
        var result = await Sut.ActivateAsync(new ActivateTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripConstants.Active);
        result.Value.StartedOn.Should().Be(StartedOn);
        await MockTripRepository.DidNotReceive().ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TripStatusEnum.Active, false)]
    [InlineData(TripStatusEnum.Completed, true)]
    [InlineData(TripStatusEnum.Cancelled, true)]
    public async Task ItShouldRefuseToActivateATripThatWasNeverPlannedOrIsAlreadyOver(
        TripStatusEnum status,
        bool withPlan)
    {
        // Arrange
        var trip = StoredTrip(
            status: status,
            plannedStartOn: withPlan ? PlannedStartOn : null,
            endedOn: status == TripStatusEnum.Completed ? StartedOn.AddHours(2) : null);
        MockTripAccessService.GivenOwner(trip, CurrentUserId);

        // Act
        var result = await Sut.ActivateAsync(new ActivateTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        await MockTripRepository.DidNotReceive().ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        var planned = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripAccessService.GivenOwner(planned, CurrentUserId);
        MockTripRepository.ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip>(new TripAlreadyActiveError()));

        // Act
        var result = await Sut.ActivateAsync(new ActivateTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripAlreadyActiveError>();
    }
}
