using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripListViewModelTests;

public sealed class WhenTestingLoad : BaseTripListViewModelTest
{
    [Fact]
    public async Task ItShouldShowTheEmptyStateAndOfferStartWhenThereAreNoTrips()
    {
        // Arrange
        ServeHistory();
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.IsEmpty.Should().BeTrue();
        Sut.CanStart.Should().BeTrue();
        Sut.IsLoading.Should().BeFalse();
        Sut.HasNotice.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTellTheUserTheyAreOfflineAndNotAskForInvitations()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        ServeHistory(serverUnavailable: true, trips: Summary(Guid.NewGuid()));
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.NoticeText.Should().Be(Text["TripListOffline"]);
        Sut.Trips.Should().ContainSingle();
        Sut.HasInvitations.Should().BeFalse();
        await MockParticipants.DidNotReceive().ListInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailedServerLoadWithoutPretendingThereAreNoTrips()
    {
        // Arrange
        ServeHistory(serverUnavailable: true);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.NoticeText.Should().Be(Text["TripListLoadFailed"]);
        Sut.HasNotice.Should().BeTrue();
        await MockParticipants.DidNotReceive().ListInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferStartWhileTheAccountOwnsAnActiveTrip()
    {
        // Arrange
        ServeHistory(trips: Summary(Guid.NewGuid()));
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.CanStart.Should().BeFalse();
    }

    [Theory]
    [InlineData(TripParticipantConstants.Participant, TripConstants.Active)]
    [InlineData(TripParticipantConstants.Owner, TripConstants.Completed)]
    public async Task ItShouldStillOfferStartWhenTheActiveTripIsSharedOrTheOwnedTripIsFinished(string role, string status)
    {
        // Arrange
        ServeHistory(trips: Summary(Guid.NewGuid(), status, role));
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.CanStart.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepFinishedTripsOutOfTheActiveLabel()
    {
        // Arrange
        ServeHistory(trips: Summary(Guid.NewGuid(), TripConstants.Completed, title: "Imported"));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        var item = Sut.Trips.Single();
        item.IsActive.Should().BeFalse();
        item.IsFinished.Should().BeTrue();
        item.ShowStatus.Should().BeTrue();
        item.StatusText.Should().Be(Text["TripStatusFinished"]);
        item.ActionText.Should().Be(Text["TripViewTrip"]);
        Sut.HasPastTrips.Should().BeTrue();
        Sut.HasActiveTrips.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldOrderActiveThenUpcomingSoonestThenPastNewest()
    {
        // Arrange
        var pastNewer = Guid.NewGuid();
        var pastOlder = Guid.NewGuid();
        var upcomingSoon = Guid.NewGuid();
        var upcomingLater = Guid.NewGuid();
        var active = Guid.NewGuid();
        var later = Now.AddMonths(8);
        var soon = Now.AddDays(3);
        ServeHistory(trips:
        [
            Summary(pastOlder, TripConstants.Completed, startedOn: Now.AddMonths(-2)),
            Summary(upcomingLater, TripConstants.Planned, startedOn: later) with { PlannedStartOn = later },
            Summary(pastNewer, TripConstants.Completed, startedOn: Now.AddDays(-1)),
            Summary(upcomingSoon, TripConstants.Planned, startedOn: soon) with { PlannedStartOn = soon },
            Summary(active, TripConstants.Active, startedOn: Now.AddHours(-2))
        ]);

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.ActiveTrips.Select(trip => trip.Id).Should().Equal(active);
        Sut.UpcomingTrips.Select(trip => trip.Id).Should().Equal(upcomingSoon, upcomingLater);
        Sut.PastTrips.Select(trip => trip.Id).Should().Equal(pastNewer, pastOlder);
        Sut.Trips.Select(trip => trip.Id).Should().Equal(active, upcomingSoon, upcomingLater, pastNewer, pastOlder);
    }

    [Fact]
    public async Task ItShouldDescribeEachTripLikeTheWebList()
    {
        // Arrange
        var active = Guid.NewGuid();
        var shared = Guid.NewGuid();
        ServeHistory(pending: new HashSet<Guid> { active }, trips:
        [
            Summary(active, catches: 2, photographs: 1, notes: 0, title: "Corrib weekend"),
            Summary(shared, TripConstants.Completed, TripParticipantConstants.Participant, catches: 1, photographs: 0, notes: 3)
        ]);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        var first = Sut.Trips[0];
        first.Id.Should().Be(active);
        first.DateLabel.Should().Be("31 Oct 2026");
        first.Title.Should().Be("Corrib weekend");
        first.HasTitle.Should().BeTrue();
        first.IsActive.Should().BeTrue();
        first.IsPending.Should().BeTrue();
        first.CatchesText.Should().Be("2 catches recorded");
        first.PhotographsText.Should().Be(Text["TripListPhotographsOne"]);
        first.NotesText.Should().Be(Text["TripListNotesNone"]);
        first.ActionText.Should().Be(Text["TripUpdate"]);
        var second = Sut.Trips[1];
        second.IsShared.Should().BeTrue();
        second.IsActive.Should().BeFalse();
        second.IsPending.Should().BeFalse();
        second.CatchesText.Should().Be(Text["TripCatchesOne"]);
        second.NotesText.Should().Be("3 notes");
        second.ActionText.Should().Be(Text["TripViewTrip"]);
    }

    [Fact]
    public async Task ItShouldListInvitationsWithTheInviterNameAndPlaceOrTitleSummary()
    {
        // Arrange
        var named = Guid.NewGuid();
        var anonymous = Guid.NewGuid();
        MockParticipants.ListInvitationsAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripInvitationDto>>.Succeeded(
        [
            new TripInvitationDto(named, FriendUserId, "Sam", "Corrib", "Lough Corrib", Now, Now),
            new TripInvitationDto(anonymous, FriendUserId, null, null, null, Now, Now)
        ]));
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasInvitations.Should().BeTrue();
        Sut.Invitations[0].TripId.Should().Be(named);
        Sut.Invitations[0].FromText.Should().Be("Sam invited you to fish this trip.");
        Sut.Invitations[0].Summary.Should().Be("Lough Corrib · 31/10/2026");
        Sut.Invitations[1].FromText.Should().Be("Another angler invited you to fish this trip.");
        Sut.Invitations[1].Summary.Should().Be("31/10/2026");
    }

    [Fact]
    public async Task ItShouldClearInvitationsWhenTheyCannotBeLoaded()
    {
        // Arrange
        MockParticipants.ListInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<TripInvitationDto>>.Failed(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.Unavailable));
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasInvitations.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldStopQuietlyWhenTheSessionEndsWhileLoading()
    {
        // Arrange
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns<FishingLogBook.Mobile.Features.Trips.Models.TripHistoryModel>(_ => throw new OperationCanceledException());
        // Act
        var act = () => Sut.LoadAsync(CancellationToken.None);
        // Assert
        await act.Should().NotThrowAsync();
        Sut.IsLoading.Should().BeFalse();
    }
}
