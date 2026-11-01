using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingState : BaseTripParticipantsViewModelTest
{
    [Fact]
    public void ItShouldStartWithNothingToShowOrSearch()
    {
        // Arrange
        // Act
        // Assert
        Sut.HasMessage.Should().BeFalse();
        Sut.MessageText.Should().BeEmpty();
        Sut.CanSearch.Should().BeFalse();
        Sut.CanInvite.Should().BeFalse();
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldLabelADeclinedInvitation()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend(TripParticipantConstants.Declined)));
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.Participants.Select(participant => participant.Status).Should().Equal(Text["TripParticipantOwner"], Text["TripParticipantDeclined"]);
    }

    [Fact]
    public async Task ItShouldOfferEveryResultWhenNobodyIsSignedInToExclude()
    {
        // Arrange
        MockSession.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockLookup.FindAsync("sam", Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<AnglerSummaryDto>>.Succeeded([new AnglerSummaryDto(FriendId, "Sam", null, null, null)]));
        Sut.SearchText = "sam";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        // Assert
        Sut.SearchResults.Select(angler => angler.UserId).Should().Equal(FriendId);
    }

    [Fact]
    public async Task ItShouldIgnoreASecondInviteWhileOneIsStillRunning()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        var release = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        MockParticipants.InviteAsync(TripId, PendingId, Arg.Any<CancellationToken>()).Returns(release.Task);
        var first = Sut.InviteAsync(PendingId, CancellationToken.None);
        // Act
        await Sut.InviteAsync(PendingId, CancellationToken.None);
        release.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await first;
        // Assert
        await MockParticipants.Received(1).InviteAsync(TripId, PendingId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSearchWhileAnInviteIsRunningAndShouldClearTheChangedFlagOnReload()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        var release = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        MockParticipants.InviteAsync(TripId, PendingId, Arg.Any<CancellationToken>()).Returns(release.Task);
        Sut.SearchText = "sam";
        var inviting = Sut.InviteAsync(PendingId, CancellationToken.None);
        SearchText();
        var canSearchWhileBusy = Sut.CanSearch;
        release.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await inviting;
        var changed = Sut.Changed;
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        canSearchWhileBusy.Should().BeFalse();
        changed.Should().BeTrue();
        Sut.Changed.Should().BeFalse();
    }

    private void SearchText()
    {
        Sut.SearchText = "samuel";
    }
}
