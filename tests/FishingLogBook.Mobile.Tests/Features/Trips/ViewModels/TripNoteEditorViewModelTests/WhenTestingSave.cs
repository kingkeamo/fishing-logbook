using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripNoteEditorViewModelTests;

public sealed class WhenTestingSave : BaseTripNoteEditorViewModelTest
{
    [Fact]
    public async Task ItShouldNotSaveABlankNote()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        Sut.NoteText = " ";
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSaveATimeOutsideTheTripWindow()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        Sut.NoteText = "Fish rising";
        Sut.NoteTime = TimeSpan.FromHours(8);
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAnErrorAndKeepTheTextWhenTheNoteCannotBeSaved()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        Sut.NoteText = "Fish rising";
        MockTrips.SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), false, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ArgumentException()));
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        Sut.SaveFailed.Should().BeTrue();
        Sut.FailureText.Should().Be(Text["TripNoteAddFailed"]);
        Sut.NoteText.Should().Be("Fish rising");
        Sut.IsSaving.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepTheOriginalInstantWhenOnlyTheTextChanges()
    {
        // Arrange
        InitializeEditing(Trip(), Note(NoteId, ViewerId, "Original", Recorded));
        Sut.NoteText = "  Corrected  ";
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        await MockTrips.Received(1).SaveNoteAsync(
            Arg.Any<LocalTripRecordModel>(),
            Arg.Is<TripNoteDto>(note => note.Id == NoteId && note.TripId == TripId && note.Text == "Corrected"
                && note.RecordedOn == Recorded && note.CreatedByUserId == ViewerId),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheNewTimeWhenItIsChangedAndReuseTheNoteId()
    {
        // Arrange
        InitializeEditing(Trip(), Note(NoteId, ViewerId, "Original", Recorded));
        Sut.NoteTime = TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30));
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        await MockTrips.Received(1).SaveNoteAsync(
            Arg.Any<LocalTripRecordModel>(),
            Arg.Is<TripNoteDto>(note => note.Id == NoteId && note.Text == "Original" && note.RecordedOn == new DateTimeOffset(2026, 10, 31, 10, 30, 0, TimeSpan.Zero)),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordANewNoteAtTheCurrentTimeWithANewIdAsTheViewer()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        Sut.NoteText = "Fish rising";
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        await MockTrips.Received(1).SaveNoteAsync(
            Arg.Any<LocalTripRecordModel>(),
            Arg.Is<TripNoteDto>(note => note.Id != Guid.Empty && note.Text == "Fish rising" && note.RecordedOn == Now && note.CreatedByUserId == ViewerId),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordABackdatedNoteAtTheChosenTime()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        Sut.NoteText = "Fish rising";
        Sut.NoteTime = TimeSpan.FromHours(10);
        // Act
        await Sut.SaveAsync(CancellationToken.None);
        // Assert
        await MockTrips.Received(1).SaveNoteAsync(
            Arg.Any<LocalTripRecordModel>(),
            Arg.Is<TripNoteDto>(note => note.RecordedOn == new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero)),
            false,
            Arg.Any<CancellationToken>());
    }
}
