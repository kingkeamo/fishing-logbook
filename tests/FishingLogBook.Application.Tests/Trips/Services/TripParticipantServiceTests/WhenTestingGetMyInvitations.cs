using AwesomeAssertions;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class WhenTestingGetMyInvitations : BaseTripParticipantServiceTest
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.Parse("2026-09-12T06:00:00Z");

    [Fact]
    public async Task ItShouldRejectAnUnresolvedCaller()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockTripParticipantRepository.DidNotReceive().GetPendingInvitationsByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIncludeThePlannedStartOfAnInvitationToAPlannedTrip()
    {
        // Arrange
        GivenPendingInvitation(Trip(OtherUserId, TripStatusEnum.Planned, PlannedStartOn));

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var invitation = result.Value.Should().ContainSingle().Subject;
        invitation.TripId.Should().Be(TripId);
        invitation.PlannedStartOn.Should().Be(PlannedStartOn);
        invitation.OwnerUserId.Should().Be(OtherUserId);
    }

    [Fact]
    public async Task ItShouldLeaveThePlannedStartEmptyForAnInvitationToAStartedTrip()
    {
        // Arrange
        GivenPendingInvitation(Trip(OtherUserId, TripStatusEnum.Active));

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle().Which.PlannedStartOn.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldHideAnInvitationToACancelledTrip()
    {
        // Arrange
        GivenPendingInvitation(Trip(OtherUserId, TripStatusEnum.Cancelled, PlannedStartOn));

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnTheTripLookupFailure()
    {
        // Arrange
        GivenPendingInvitation(Trip(OtherUserId, TripStatusEnum.Planned, PlannedStartOn));
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip?>("Failed to save the trip."));

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    private void GivenPendingInvitation(Trip trip)
    {
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(trip));
        MockTripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(
                [Membership(CurrentUserId, TripParticipantStatusEnum.Pending)]));
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(
                new Dictionary<Guid, AnglerSummaryDto>
                {
                    [OtherUserId] = new(OtherUserId, "Sam", null, null, null)
                }));
    }

    private static Trip Trip(Guid ownerUserId, TripStatusEnum status, DateTimeOffset? plannedStartOn = null)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = plannedStartOn ?? StartedOn,
            PlannedStartOn = plannedStartOn,
            PlaceName = "Lough Corrib"
        };
    }
}
