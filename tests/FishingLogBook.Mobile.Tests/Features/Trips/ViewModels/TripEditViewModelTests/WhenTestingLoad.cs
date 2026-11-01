using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public sealed class WhenTestingLoad : BaseTripEditViewModelTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Two = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid NoteOne = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid NoteTwo = Guid.Parse("b0000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldSayTheTripIsUnavailableWhenItCannotBeFound()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns((LocalTripRecordModel?)null);
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.NotFound.Should().BeTrue();
        Sut.ShowContent.Should().BeFalse();
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldPrefillTheTitleLocationAndSummaryFromTheTrip()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(title: "Corrib", place: "Lough Corrib"));
        // Assert
        Sut.EditTitle.Should().Be("Corrib");
        Sut.EditPlace.Should().Be("Lough Corrib");
        Sut.SummaryText.Should().Be("31 Oct 2026 · 09:00");
        Sut.CanManageTrip.Should().BeTrue();
        Sut.ShowContent.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotOfferManagementOfAFinishedTripOrToAParticipant()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)));
        var finished = Sut.CanManageTrip;
        await LoadAsync(Trip(role: TripParticipantConstants.Participant, owner: FriendId));
        // Assert
        finished.Should().BeFalse();
        Sut.CanManageTrip.Should().BeFalse();
        Sut.CanAddNotes.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepUnsavedTitleAndLocationEditsWhenTheTripIsReloaded()
    {
        // Arrange
        await LoadAsync(Trip(title: "Corrib"));
        Sut.EditTitle = "Corrib weekend";
        Sut.EditPlace = "Lough Mask";
        // Act
        await Sut.ReloadAsync(CancellationToken.None);
        // Assert
        Sut.EditTitle.Should().Be("Corrib weekend");
        Sut.EditPlace.Should().Be("Lough Mask");
    }

    [Fact]
    public async Task ItShouldListNotesPhotographsAndOnlyTheViewersOwnCatchesWithWhatTheyMayChange()
    {
        // Arrange
        var photographs = new[]
        {
            new TripPhotographViewDto(One, "image/jpeg", Now.AddHours(-2), "https://img/1.jpg") { ContributedByUserId = ViewerId },
            new TripPhotographViewDto(Two, "image/jpeg", Now.AddHours(-1), "https://img/2.jpg") { ContributedByUserId = FriendId }
        };
        // Act
        await LoadAsync(Trip(
            notes: [Note(NoteOne, ViewerId, "Mine", Now.AddHours(-2)), Note(NoteTwo, FriendId, "Theirs", Now.AddHours(-1))],
            photographs: photographs,
            catches: [Catch(One, ViewerId), Catch(Two, FriendId), Catch(Guid.NewGuid(), ViewerId, recordedBy: FriendId)]));
        // Assert
        Sut.Notes.Select(note => (note.Id, note.CanChange)).Should().Equal((NoteOne, true), (NoteTwo, false));
        Sut.Photographs.Select(photograph => (photograph.Id, photograph.CanRemove)).Should().Equal((One, true), (Two, false));
        Sut.Catches.Select(item => item.Id).Should().Equal(One);
        Sut.Catches[0].Measurements.Should().Be("2.5 kg");
        Sut.NoCatches.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldStopQuietlyWhenTheSessionEndsWhileLoading()
    {
        // Arrange
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns<LocalTripRecordModel?>(_ => throw new OperationCanceledException());
        // Act
        var act = () => Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        await act.Should().NotThrowAsync();
        Sut.IsLoading.Should().BeFalse();
    }
}
