using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingInvite : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldNotInviteWhenTheViewerIsNotTheOwner()
    {
        // Arrange
        // Act
        await Sut.InviteAsync(PendingId, CancellationToken.None);
        await Sut.RemoveAsync(PendingId, CancellationToken.None);
        // Assert
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ItShouldShowAnErrorWhenInvitingFailsAndKeepTheParticipantsUnchanged()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockParticipants.InviteAsync(TripId, PendingId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.ValidationFailed));
        // Act
        await Sut.InviteAsync(PendingId, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripInviteFailed"]);
        Sut.Participants.Should().ContainSingle();
    }


    [Fact]
    public async Task ItShouldShowTheInvitedAnglerAsPendingAfterInviting()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockParticipants.InviteAsync(TripId, PendingId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Succeeded(
            Participants(TripParticipantConstants.Owner, Owner(), new TripParticipantDto(PendingId, TripParticipantConstants.Pending, "Pat", null, Now))));
        Sut.SearchText = "pat";
        // Act
        await Sut.InviteAsync(PendingId, CancellationToken.None);
        // Assert
        Sut.Participants.Select(participant => participant.Status).Should().Equal(Text["TripParticipantOwner"], Text["TripParticipantPending"]);
        Sut.SearchResults.Should().BeEmpty();
        Sut.SearchText.Should().BeEmpty();
        Sut.HasMessage.Should().BeFalse();
        await MockParticipants.Received(1).InviteAsync(TripId, PendingId, Arg.Any<CancellationToken>());
    }
}
