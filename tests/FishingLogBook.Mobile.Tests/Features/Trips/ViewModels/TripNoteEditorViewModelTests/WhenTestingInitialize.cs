using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripNoteEditorViewModelTests;

public sealed class WhenTestingInitialize : BaseTripNoteEditorViewModelTest
{
    [Fact]
    public void ItShouldRejectATimeBeforeTheTripStarted()
    {
        // Arrange
        InitializeEditing(Trip(), Note(NoteId, ViewerId, at: Recorded));
        // Act
        Sut.NoteTime = TimeSpan.FromHours(8);
        // Assert
        Sut.HasWhenError.Should().BeTrue();
        Sut.WhenText.Should().Be(string.Format(Text.Culture, Text["TripNoteBeforeTripStart"], Now.AddHours(-3).DateTime.ToString("g", Text.Culture)));
        Sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRejectATimeInTheFutureOnAnActiveTrip()
    {
        // Arrange
        InitializeEditing(Trip(), Note(NoteId, ViewerId, at: Recorded));
        // Act
        Sut.NoteTime = TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(1));
        // Assert
        Sut.WhenText.Should().Be(Text["TripNoteAfterNow"]);
        Sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRejectATimeAfterAFinishedTripEnded()
    {
        // Arrange
        var ended = Now.AddHours(-1);
        InitializeEditing(Trip(TripConstants.Completed, endedOn: ended), Note(NoteId, ViewerId, at: Now.AddHours(-2)));
        // Act
        Sut.NoteTime = TimeSpan.FromHours(11).Add(TimeSpan.FromMinutes(1));
        // Assert
        Sut.WhenText.Should().Be(string.Format(Text.Culture, Text["TripNoteAfterTripEnd"], ended.DateTime.ToString("g", Text.Culture)));
        Sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAcceptTheExactTripStartAndTheCurrentMinute()
    {
        // Arrange
        InitializeEditing(Trip(), Note(NoteId, ViewerId, at: Recorded));
        // Act
        Sut.NoteTime = TimeSpan.FromHours(9);
        var atStart = Sut.HasWhenError;
        Sut.NoteTime = TimeSpan.FromHours(12);
        // Assert
        atStart.Should().BeFalse();
        Sut.HasWhenError.Should().BeFalse();
        Sut.CanSave.Should().BeTrue();
    }

    [Fact]
    public void ItShouldNotAllowABlankNote()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        // Act
        Sut.NoteText = "   ";
        // Assert
        Sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public void ItShouldPrefillTheTextAndTimeAndUseTheEditHeadingForAnExistingNote()
    {
        // Arrange
        // Act
        InitializeEditing(Trip(), Note(NoteId, ViewerId, "Original", Recorded));
        // Assert
        Sut.IsEditing.Should().BeTrue();
        Sut.NoteText.Should().Be("Original");
        Sut.Heading.Should().Be(Text["TripNoteEdit"]);
        Sut.SaveText.Should().Be(Text["Save"]);
        Sut.NoteDate.Should().Be(new DateTime(2026, 10, 31));
        Sut.NoteTime.Should().Be(new TimeSpan(11, 0, 0));
    }

    [Fact]
    public void ItShouldStartANewNoteEmptyAtTheCurrentTimeWithTheAddHeading()
    {
        // Arrange
        // Act
        Sut.Initialize(Trip(), null);
        // Assert
        Sut.IsEditing.Should().BeFalse();
        Sut.NoteText.Should().BeEmpty();
        Sut.Heading.Should().Be(Text["TripNoteAdd"]);
        Sut.NoteTime.Should().Be(new TimeSpan(12, 0, 0));
        Sut.HasWhenError.Should().BeFalse();
    }
}
