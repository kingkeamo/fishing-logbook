using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingLoadReset : BaseTripParticipantsViewModelTest
{
    private static readonly Guid OtherTripId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task ItShouldBlockSearchInviteAndRemoveUntilTheFirstLoadHasFinished()
    {
        // Arrange
        var pending = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var load = Sut.LoadAsync(TripId, CancellationToken.None);
        Sut.SearchText = "sam";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        await Sut.InviteAsync(FriendId, CancellationToken.None);
        await Sut.RemoveAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.IsLoaded.Should().BeFalse();
        Sut.CanSearch.Should().BeFalse();
        Sut.CanInvite.Should().BeFalse();
        await MockLookup.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        pending.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await load;
    }

    [Fact]
    public async Task ItShouldBlockSearchInviteAndRemoveWhenTheLoadFailed()
    {
        // Arrange
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        Sut.SearchText = "sam";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        await Sut.InviteAsync(FriendId, CancellationToken.None);
        await Sut.RemoveAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.IsLoaded.Should().BeFalse();
        Sut.CanInvite.Should().BeFalse();
        Sut.MessageText.Should().Be(Text["TripParticipantsLoadFailed"]);
        await MockLookup.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepNothingFromThePreviousTripWhenTheNextTripFailsToLoad()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        MockLookup.FindAsync("angler", Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<AnglerSummaryDto>>.Succeeded(
            [new AnglerSummaryDto(PendingId, "Pat", null, null, null)]));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        Sut.SearchText = "angler";
        await Sut.SearchAsync(CancellationToken.None);
        MockParticipants.GetAsync(OtherTripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        await Sut.LoadAsync(OtherTripId, CancellationToken.None);
        // Assert
        Sut.Participants.Should().BeEmpty();
        Sut.SearchResults.Should().BeEmpty();
        Sut.SearchText.Should().BeEmpty();
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowOwnerOnlyNote.Should().BeFalse();
        Sut.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotKeepOwnerPermissionsAfterAnOfflineReload()
    {
        // Arrange
        await LoadOwnedTripAsync();
        MockConnectivity.IsOnline.Returns(false);
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        await Sut.InviteAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.CanInvite.Should().BeFalse();
        Sut.Participants.Should().BeEmpty();
        Sut.MessageText.Should().Be(Text["TripParticipantsLoadFailed"]);
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotKeepOwnerPermissionsWhenTheNextTripOnlyAllowsAParticipant()
    {
        // Arrange
        await LoadOwnedTripAsync();
        MockParticipants.GetAsync(OtherTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Participant, Owner(), Friend())));
        // Act
        await Sut.LoadAsync(OtherTripId, CancellationToken.None);
        await Sut.InviteAsync(PendingId, CancellationToken.None);
        // Assert
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowOwnerOnlyNote.Should().BeTrue();
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreASearchThatFinishesAfterAnotherTripLoaded()
    {
        // Arrange
        await LoadOwnedTripAsync();
        var search = new TaskCompletionSource<ApiResultModel<List<AnglerSummaryDto>>>();
        MockLookup.FindAsync("angler", Arg.Any<CancellationToken>()).Returns(search.Task);
        Sut.SearchText = "angler";
        var searching = Sut.SearchAsync(CancellationToken.None);
        MockParticipants.GetAsync(OtherTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await Sut.LoadAsync(OtherTripId, CancellationToken.None);
        // Act
        search.SetResult(ApiResultModel<List<AnglerSummaryDto>>.Succeeded([new AnglerSummaryDto(PendingId, "Pat", null, null, null)]));
        await searching;
        // Assert
        Sut.SearchResults.Should().BeEmpty();
        Sut.HasMessage.Should().BeFalse();
        Sut.Participants.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldIgnoreAnInviteThatFinishesAfterAnotherTripLoaded()
    {
        // Arrange
        await LoadOwnedTripAsync();
        var invite = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        MockParticipants.InviteAsync(TripId, FriendId, Arg.Any<CancellationToken>()).Returns(invite.Task);
        var inviting = Sut.InviteAsync(FriendId, CancellationToken.None);
        MockParticipants.GetAsync(OtherTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await Sut.LoadAsync(OtherTripId, CancellationToken.None);
        // Act
        invite.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner(), Friend())));
        await inviting;
        // Assert
        Sut.Participants.Should().ContainSingle();
        Sut.Changed.Should().BeFalse();
        Sut.CanSearch.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowOnlyTheLatestTripWhenTwoLoadsOverlapAndTheOlderFinishesLast()
    {
        // Arrange
        var older = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(older.Task);
        MockParticipants.GetAsync(OtherTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Participant, Owner())));
        var olderLoad = Sut.LoadAsync(TripId, CancellationToken.None);
        await Sut.LoadAsync(OtherTripId, CancellationToken.None);
        // Act
        older.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner(), Friend())));
        await olderLoad;
        // Assert
        Sut.Participants.Should().ContainSingle();
        Sut.CanInvite.Should().BeFalse();
        Sut.IsLoaded.Should().BeTrue();
    }
}
