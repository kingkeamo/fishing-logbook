using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public sealed class WhenTestingLoad : BaseTripDetailViewModelTest
{
    [Fact]
    public async Task ItShouldSayTheTripIsUnavailableWhenItCannotBeFound()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns((FishingLogBook.Mobile.Features.Trips.Offline.Models.LocalTripRecordModel?)null);
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.NotFound.Should().BeTrue();
        Sut.ShowContent.Should().BeFalse();
        Sut.IsLoading.Should().BeFalse();
        Sut.CanRecordCatch.Should().BeFalse();
        Sut.CanAddNotes.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldLetThePersonWhoStartedAnActiveTripManageAndRecordIntoIt()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(title: "Corrib weekend", place: "Lough Corrib"));
        // Assert
        Sut.ShowContent.Should().BeTrue();
        (Sut.CanManageTrip, Sut.CanRecordCatch, Sut.CanAddNotes, Sut.ShowBackToLogbook).Should().Be((true, true, true, false));
        (Sut.HasTitle, Sut.TitleText, Sut.HasPlace, Sut.PlaceText).Should().Be((true, "Corrib weekend", true, "Lough Corrib"));
        Sut.DateHeading.Should().Be("31 Oct 2026");
        Sut.StartedText.Should().Be("09:00");
        Sut.ElapsedText.Should().Be("3h 0m");
        Sut.StatusText.Should().Be(Text["TripActiveLabel"]);
    }

    [Fact]
    public async Task ItShouldLetAParticipantRecordAndAddNotesButNotManageTheTrip()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(role: TripParticipantConstants.Participant, owner: FriendId));
        // Assert
        (Sut.CanManageTrip, Sut.CanRecordCatch, Sut.CanAddNotes, Sut.ShowBackToLogbook).Should().Be((false, true, true, true));
    }

    [Fact]
    public async Task ItShouldKeepNotesButStopCatchesAndManagementOnAFinishedTrip()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)));
        // Assert
        (Sut.CanManageTrip, Sut.CanRecordCatch, Sut.CanAddNotes).Should().Be((false, false, true));
        Sut.StatusText.Should().Be(Text["TripStatusFinished"]);
        Sut.ElapsedText.Should().Be("2h 0m");
        Sut.Timeline.Select(row => row.Kind).Should().Equal(TripTimelineKindEnum.Started, TripTimelineKindEnum.Finished);
    }

    [Fact]
    public async Task ItShouldGiveNoAccessToSomeoneWhoCannotContribute()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(role: TripParticipantConstants.None, owner: FriendId));
        // Assert
        (Sut.CanManageTrip, Sut.CanRecordCatch, Sut.CanAddNotes).Should().Be((false, false, false));
    }

    [Fact]
    public async Task ItShouldShowCatchesNotesAndTheStartInChronologicalOrderWithContributorsOnSharedTrips()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var catchId = Guid.NewGuid();
        var trip = Trip(
            notes: [Note(noteId, FriendId, "Water dropped", Now.AddHours(-2))],
            contributors:
            [
                new TripContributorDto(ViewerId, "Me", null) { IsOwner = true },
                new TripContributorDto(FriendId, "Sam", null)
            ],
            catches:
            [
                new TripCatchSummaryDto(catchId, Now.AddHours(-1)) { SpeciesName = "Pike", Weight = 2.5m, Length = 80m, CaughtByUserId = FriendId, RecordedByUserId = ViewerId }
            ]);
        // Act
        await LoadAsync(trip);
        // Assert
        Sut.Timeline.Select(row => row.Kind).Should().Equal(TripTimelineKindEnum.Started, TripTimelineKindEnum.Note, TripTimelineKindEnum.Catch);
        var note = Sut.Timeline[1];
        note.Detail.Should().Be("Water dropped");
        note.ContributorText.Should().Be("Sam");
        note.CanDelete.Should().BeFalse();
        var caught = Sut.Timeline[2];
        caught.Title.Should().Be("Pike");
        caught.Detail.Should().Be("2.5 kg · 80 cm");
        caught.RecordedByText.Should().Be("Recorded by Me");
        caught.CatchId.Should().Be(catchId);
        caught.CanOpenCatch.Should().BeFalse();
        Sut.CatchSummary.Should().Be(Text["TripCatchesOne"]);
        Sut.NoteSummary.Should().Be(Text["TripListNotesOne"]);
    }

    [Fact]
    public async Task ItShouldOnlyLetTheViewerOpenACatchTheyBothCaughtAndRecorded()
    {
        // Arrange
        var own = Guid.NewGuid();
        var recordedForAnother = Guid.NewGuid();
        var trip = Trip(catches:
        [
            new TripCatchSummaryDto(own, Now.AddHours(-2)) { SpeciesName = "Pike", CaughtByUserId = ViewerId, RecordedByUserId = ViewerId },
            new TripCatchSummaryDto(recordedForAnother, Now.AddHours(-1)) { SpeciesName = "Perch", CaughtByUserId = FriendId, RecordedByUserId = ViewerId }
        ]);
        // Act
        await LoadAsync(trip);
        // Assert
        Sut.Timeline.Where(row => row.IsCatch).Select(row => (row.CatchId, row.CanOpenCatch)).Should().Equal((own, true), (recordedForAnother, false));
    }

    [Fact]
    public async Task ItShouldShowAnUnsyncedLocalCatchOnTheTripWithItsPendingStatus()
    {
        // Arrange
        var localId = Guid.NewGuid();
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new FishingLogBook.Mobile.Features.Catch.Offline.Models.LocalCatchRecordModel
            {
                Id = localId, UserId = ViewerId, TripId = TripId, CaughtByUserId = ViewerId, RecordedByUserId = ViewerId,
                CaughtOn = Now.AddMinutes(-30), SpeciesName = "Brown Trout", Revision = 1,
                Lifecycle = FishingLogBook.Mobile.Features.Catch.Offline.Enums.LocalCatchLifecycleEnum.Saved, CreatedOn = Now, UpdatedOn = Now,
                Photographs = []
            }
        ]);
        // Act
        await LoadAsync(Trip());
        // Assert
        var row = Sut.Timeline.Single(item => item.IsCatch);
        row.CatchId.Should().Be(localId);
        row.Title.Should().Be("Brown Trout");
        row.PendingText.Should().Be(Text["CatchSyncWaiting"]);
        Sut.CatchSummary.Should().Be(Text["TripCatchesOne"]);
    }

    [Fact]
    public async Task ItShouldStopQuietlyWhenTheSessionEndsWhileLoading()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns<FishingLogBook.Mobile.Features.Trips.Offline.Models.LocalTripRecordModel?>(_ => throw new OperationCanceledException());
        // Act
        var act = () => Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        await act.Should().NotThrowAsync();
        Sut.IsLoading.Should().BeFalse();
        Sut.ShowContent.Should().BeFalse();
    }
}
