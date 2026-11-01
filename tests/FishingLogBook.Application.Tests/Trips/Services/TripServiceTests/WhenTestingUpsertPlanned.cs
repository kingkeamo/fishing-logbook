using AwesomeAssertions;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingUpsertPlanned : BaseTripServiceTest
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.Parse("2026-09-12T06:00:00Z");

    [Fact]
    public async Task ItShouldRejectAPlannedTripWithoutAPlannedStart()
    {
        // Arrange
        var args = UpsertArgs(status: TripConstants.Planned, startedOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLifecycleInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPlannedTripThatHasAnEnd()
    {
        // Arrange
        var args = UpsertArgs(
            status: TripConstants.Planned,
            plannedStartOn: PlannedStartOn,
            endedOn: PlannedStartOn.AddHours(2));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLifecycleInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectCreatingACancelledTrip()
    {
        // Arrange
        var args = UpsertArgs(status: TripConstants.Cancelled, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TripStatusEnum.Active, TripConstants.Planned)]
    [InlineData(TripStatusEnum.Completed, TripConstants.Planned)]
    [InlineData(TripStatusEnum.Planned, TripConstants.Completed)]
    [InlineData(TripStatusEnum.Cancelled, TripConstants.Planned)]
    [InlineData(TripStatusEnum.Cancelled, TripConstants.Active)]
    [InlineData(TripStatusEnum.Cancelled, TripConstants.Completed)]
    [InlineData(TripStatusEnum.Active, TripConstants.Cancelled)]
    [InlineData(TripStatusEnum.Completed, TripConstants.Cancelled)]
    public async Task ItShouldRejectAnInvalidLifecycleTransition(TripStatusEnum existingStatus, string requested)
    {
        // Arrange
        var existing = StoredTrip(
            status: existingStatus,
            plannedStartOn: PlannedStartOn,
            endedOn: existingStatus == TripStatusEnum.Completed ? StartedOn.AddHours(2) : null);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenOwner(existing, CurrentUserId);
        var args = requested switch
        {
            TripConstants.Planned or TripConstants.Cancelled => UpsertArgs(status: requested, plannedStartOn: PlannedStartOn),
            TripConstants.Completed => UpsertArgs(status: requested, endedOn: StartedOn.AddHours(3)),
            _ => UpsertArgs(status: requested)
        };

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAParticipantEditingAPlannedTrip()
    {
        // Arrange
        var existing = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenParticipant(existing, OtherUserId);
        var args = UpsertArgs(userId: OtherUserId, status: TripConstants.Planned, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnerActionRequiredError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAParticipantActivatingAPlannedTrip()
    {
        // Arrange
        var existing = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenParticipant(existing, OtherUserId);
        var args = UpsertArgs(userId: OtherUserId, status: TripConstants.Active);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnerActionRequiredError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCreateAPlannedTripUsingThePlannedStartForBothStartValues()
    {
        // Arrange
        var args = UpsertArgs(
            status: TripConstants.Planned,
            plannedStartOn: PlannedStartOn,
            startedOn: StartedOn,
            title: "  Corrib weekend ");

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripConstants.Planned);
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn);
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.Status == TripStatusEnum.Planned
                && trip.PlannedStartOn == PlannedStartOn
                && trip.StartedOn == PlannedStartOn
                && trip.Title == "Corrib weekend"
                && trip.OwnerUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
        await MockTripAccessService.DidNotReceive().RequireOwnerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheOwnerToEditAPlannedTrip()
    {
        // Arrange
        var existing = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenOwner(existing, CurrentUserId);
        var args = UpsertArgs(
            status: TripConstants.Planned,
            plannedStartOn: PlannedStartOn.AddHours(3),
            placeName: "Lough Mask");

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripAccessService.Received(1).RequireOwnerAsync(TripId, Arg.Any<CancellationToken>());
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.PlannedStartOn == PlannedStartOn.AddHours(3)
                && trip.StartedOn == PlannedStartOn.AddHours(3)
                && trip.PlaceName == "Lough Mask"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheOwnerToCancelAPlannedTrip()
    {
        // Arrange
        var existing = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenOwner(existing, CurrentUserId);
        var args = UpsertArgs(status: TripConstants.Cancelled, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripConstants.Cancelled);
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.Status == TripStatusEnum.Cancelled && trip.PlannedStartOn == PlannedStartOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheOwnerToActivateAPlannedTripAtTheActualStart()
    {
        // Arrange
        var existing = StoredTrip(status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenOwner(existing, CurrentUserId);
        var actualStart = PlannedStartOn.AddMinutes(20);
        var args = UpsertArgs(status: TripConstants.Active, startedOn: actualStart, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.Status == TripStatusEnum.Active
                && trip.StartedOn == actualStart
                && trip.PlannedStartOn == PlannedStartOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepExistingStartedTripsEditableAsBefore()
    {
        // Arrange
        var existing = StoredTrip();
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(existing));
        MockTripAccessService.GivenOwner(existing, CurrentUserId);
        var args = UpsertArgs(status: TripConstants.Completed, endedOn: StartedOn.AddHours(5));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.Status == TripStatusEnum.Completed && trip.EndedOn == StartedOn.AddHours(5)),
            Arg.Any<CancellationToken>());
    }
}
