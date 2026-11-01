using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingConfirmAndRemove : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldNotAskOrRemoveBeforeTheTripHasLoaded()
    {
        // Arrange
        // Act
        await Sut.ConfirmAndRemoveAsync(FriendId, CancellationToken.None);
        // Assert
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAskOrRemoveWhenTheViewerIsNotTheOwner()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Participant, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Act
        await Sut.ConfirmAndRemoveAsync(FriendId, CancellationToken.None);
        // Assert
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveNobodyWhenTheOwnerDeclinesTheConfirmation()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        // Act
        await Sut.ConfirmAndRemoveAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.Participants.Should().HaveCount(2);
        Sut.Changed.Should().BeFalse();
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureInTheModalWhenTheConfirmedRemovalFails()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockParticipants.RemoveAsync(TripId, FriendId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        await Sut.ConfirmAndRemoveAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripParticipantsActionFailed"]);
        Sut.Participants.Should().HaveCount(2);
        Sut.Changed.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotRemoveWhenAnotherTripLoadedWhileTheConfirmationWasOpen()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        var answer = new TaskCompletionSource<bool>();
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(answer.Task);
        var removing = Sut.ConfirmAndRemoveAsync(FriendId, CancellationToken.None);
        var otherTrip = Guid.NewGuid();
        MockParticipants.GetAsync(otherTrip, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner(), Friend())));
        await Sut.LoadAsync(otherTrip, CancellationToken.None);
        // Act
        answer.SetResult(true);
        await removing;
        // Assert
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Sut.Changed.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldAskWithADestructiveConfirmationThenRemoveTheChosenAngler()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockParticipants.RemoveAsync(TripId, FriendId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        // Act
        await Sut.ConfirmAndRemoveAsync(FriendId, CancellationToken.None);
        // Assert
        await MockPresenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request => request.Title == Text["TripRemoveParticipantTitle"] && request.Message == Text["TripRemoveParticipantMessage"]
                && request.Accept == Text["TripRemoveParticipant"] && request.Cancel == Text["Cancel"] && request.IsDestructive),
            Arg.Any<CancellationToken>());
        await MockParticipants.Received(1).RemoveAsync(TripId, FriendId, Arg.Any<CancellationToken>());
        Sut.Participants.Should().ContainSingle();
        Sut.Changed.Should().BeTrue();
    }
}
