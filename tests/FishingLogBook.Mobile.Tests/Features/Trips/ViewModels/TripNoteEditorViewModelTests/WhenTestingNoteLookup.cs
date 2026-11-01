using AwesomeAssertions;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripNoteEditorViewModelTests;

public sealed class WhenTestingNoteLookup : BaseTripNoteEditorViewModelTest
{
    [Fact]
    public void ItShouldOpenTheNoteWithTheGivenIdForEditing()
    {
        // Arrange
        var trip = Trip(notes: [Note(Guid.NewGuid(), ViewerId, "Other"), Note(NoteId, ViewerId, "Original", Recorded)]);
        // Act
        var opened = Sut.Initialize(trip, NoteId);
        // Assert
        opened.Should().BeTrue();
        Sut.IsEditing.Should().BeTrue();
        Sut.NoteText.Should().Be("Original");
    }

    [Fact]
    public void ItShouldOpenANewNoteWhenNoIdIsGiven()
    {
        // Arrange
        // Act
        var opened = Sut.Initialize(Trip(notes: [Note(NoteId, ViewerId)]), null);
        // Assert
        opened.Should().BeTrue();
        Sut.IsEditing.Should().BeFalse();
        Sut.NoteText.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRefuseANoteThatIsNoLongerOnTheTripAndLeaveTheEditorAsItWas()
    {
        // Arrange
        Sut.Initialize(Trip(notes: [Note(NoteId, ViewerId, "Original", Recorded)]), NoteId);
        // Act
        var opened = Sut.Initialize(Trip(), NoteId);
        // Assert
        opened.Should().BeFalse();
        Sut.IsEditing.Should().BeTrue();
        Sut.NoteText.Should().Be("Original");
    }
}
