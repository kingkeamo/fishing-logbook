using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class WhenTestingRemove : BaseTripParticipantServiceTest
{
    [Fact]
    public async Task ItShouldReportNotFoundWhenTheAnglerCannotSeeTheTrip()
    {
        // Arrange
        GivenNoAccess();

        // Act
        var result = await Sut.RemoveAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseARemovalByAParticipant()
    {
        // Arrange
        GivenParticipantView();
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Accepted);

        // Act
        var result = await Sut.RemoveAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnerActionRequiredError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenTheAnglerIsNotOnTheTrip()
    {
        // Act
        var result = await Sut.RemoveAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenTheAnglerWasAlreadyRemoved()
    {
        // Arrange
        GivenExistingMembership(
            InvitedUserId,
            TripParticipantStatusEnum.Accepted,
            removedOn: StartedOn.AddHours(1));

        // Act
        var result = await Sut.RemoveAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWithdrawAPendingInvitation()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RemoveAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == InvitedUserId
                && participant.Status == TripParticipantStatusEnum.Declined
                && !participant.IsContributing),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopAnAcceptedParticipantContributingWithoutDeletingTheirHistory()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Accepted);

        // Act
        var result = await Sut.RemoveAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == InvitedUserId
                && participant.Status == TripParticipantStatusEnum.Accepted
                && participant.RemovedOn != null
                && !participant.IsContributing),
            Arg.Any<CancellationToken>());
    }

    private static RemoveTripParticipantArgs Args(Guid? participantUserId = null)
    {
        return new RemoveTripParticipantArgs
        {
            TripId = TripId,
            ParticipantUserId = participantUserId ?? InvitedUserId
        };
    }
}
