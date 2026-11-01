using AwesomeAssertions;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Models.LocalTripRecordModelTests;

public sealed class WhenTestingFindNote
{
    [Fact]
    public void ItShouldFindTheNoteWithTheGivenIdAndNoOther()
    {
        // Arrange
        var wanted = Note(Guid.NewGuid(), ViewerId, "Wanted");
        var trip = Trip(notes: [Note(Guid.NewGuid(), ViewerId, "Other"), wanted]);
        // Act
        var found = trip.FindNote(wanted.Id);
        var missing = trip.FindNote(Guid.NewGuid());
        // Assert
        found.Should().Be(wanted);
        missing.Should().BeNull();
    }
}
