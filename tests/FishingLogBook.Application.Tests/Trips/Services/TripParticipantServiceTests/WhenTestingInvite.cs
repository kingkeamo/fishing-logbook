using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class WhenTestingInvite : BaseTripParticipantServiceTest
{
    [Fact]
    public async Task ItShouldReportNotFoundWhenTheAnglerCannotSeeTheTrip()
    {
        // Arrange
        GivenNoAccess();

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnInviteFromAParticipant()
    {
        // Arrange
        GivenParticipantView();

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnerActionRequiredError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseTheOwnerInvitingThemselves()
    {
        // Act
        var result = await Sut.InviteAsync(Args(CurrentUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantSelfInviteError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnAnglerWhoDoesNotExist()
    {
        // Arrange
        MockProfileRepository.UserExistsAsync(InvitedUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantUserNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseADuplicatePendingInvitation()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantAlreadyInvitedError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseInvitingAnAnglerWhoHasAlreadyAccepted()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Accepted);

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripParticipantAlreadyInvitedError>();
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReinviteAnAnglerWhoPreviouslyDeclined()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Declined);

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == InvitedUserId
                && participant.Status == TripParticipantStatusEnum.Pending
                && participant.RespondedOn == null
                && participant.RemovedOn == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReinviteARemovedParticipant()
    {
        // Arrange
        GivenExistingMembership(
            InvitedUserId,
            TripParticipantStatusEnum.Accepted,
            removedOn: StartedOn.AddHours(2));

        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.Status == TripParticipantStatusEnum.Pending
                && participant.RemovedOn == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCreateAPendingInvitationForAnExistingAngler()
    {
        // Act
        var result = await Sut.InviteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TripId.Should().Be(TripId);
        result.Value.Role.Should().Be(TripParticipantConstants.Owner);
        await MockProfileRepository.Received(1).UserExistsAsync(
            InvitedUserId,
            Arg.Any<CancellationToken>());
        await MockTripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.TripId == TripId
                && participant.UserId == InvitedUserId
                && participant.InvitedByUserId == CurrentUserId
                && participant.Status == TripParticipantStatusEnum.Pending
                && participant.RespondedOn == null),
            Arg.Any<CancellationToken>());
    }

    private static InviteTripParticipantArgs Args(Guid? invitedUserId = null)
    {
        return new InviteTripParticipantArgs
        {
            TripId = TripId,
            InvitedUserId = invitedUserId ?? InvitedUserId
        };
    }
}
