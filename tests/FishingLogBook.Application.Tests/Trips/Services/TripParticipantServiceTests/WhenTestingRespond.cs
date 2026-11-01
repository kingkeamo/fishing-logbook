using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class WhenTestingRespond : BaseTripParticipantServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnUnresolvedCaller()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenThereIsNoInvitation()
    {
        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripInvitationNotFoundError>();
        await MockTripParticipantRepository.Received(1).FindAsync(
            Arg.Is<FindTripParticipantArgs>(args =>
                args.TripId == TripId && args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLetAnAnglerAnswerSomebodyElsesInvitation()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripInvitationNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnInvitationThatWasAlreadyAnswered()
    {
        // Arrange
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Accepted);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantAlreadyRespondedError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnInvitationAfterTheAnglerWasRemoved()
    {
        // Arrange
        GivenExistingMembership(
            CurrentUserId,
            TripParticipantStatusEnum.Accepted,
            removedOn: StartedOn.AddHours(2));

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripInvitationNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeclineTheInvitation()
    {
        // Arrange
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(
            Args(TripParticipantStatusEnum.Declined),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == CurrentUserId
                && participant.Status == TripParticipantStatusEnum.Declined
                && participant.RespondedOn != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptTheInvitationForTheInvitedAngler()
    {
        // Arrange
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.TripId == TripId
                && participant.UserId == CurrentUserId
                && participant.Status == TripParticipantStatusEnum.Accepted
                && participant.IsContributing
                && participant.RespondedOn != null),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TripParticipantStatusEnum.Accepted)]
    [InlineData(TripParticipantStatusEnum.Declined)]
    public async Task ItShouldLetTheInviteeAnswerAnInvitationToAPlannedTrip(TripParticipantStatusEnum response)
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(OtherUserId, TripStatusEnum.Planned)));
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(Args(response), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == CurrentUserId
                && participant.TripId == TripId
                && participant.Status == response
                && participant.RespondedOn != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnInvitationToACancelledTripAsGone()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(OtherUserId, TripStatusEnum.Cancelled)));
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripInvitationNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnInvitationToAMissingTripAsGone()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripInvitationNotFoundError>();
    }

    [Fact]
    public async Task ItShouldReturnTheTripLookupFailure()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip?>("Failed to save the trip."));
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RespondAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    private static RespondToTripInvitationArgs Args(
        TripParticipantStatusEnum response = TripParticipantStatusEnum.Accepted)
    {
        return new RespondToTripInvitationArgs
        {
            TripId = TripId,
            Response = response
        };
    }
}
