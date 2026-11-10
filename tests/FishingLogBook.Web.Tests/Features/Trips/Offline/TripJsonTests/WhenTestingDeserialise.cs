using AwesomeAssertions;
using FishingLogBook.Web.Features.Trips.Offline;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.TripJsonTests;

public class WhenTestingDeserialise
{
    [Fact]
    public void ItShouldRejectANullTripDocument()
    {
        // Arrange
        // Act
        var act = () => TripJson.Deserialize("null");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Trip*could not be read*");
    }

    [Fact]
    public void ItShouldRejectANullTripNoteDocument()
    {
        // Arrange
        // Act
        var act = () => TripJson.DeserializeNote("null");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Trip note metadata could not be read.");
    }

    [Fact]
    public void ItShouldRejectANullTripPhotographDocument()
    {
        // Arrange
        // Act
        var act = () => TripJson.DeserializePhotograph("null");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Trip photograph metadata could not be read.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    public void ItShouldRejectMalformedJsonForEveryDocumentKind(string json)
    {
        // Arrange
        // Act
        var trip = () => TripJson.Deserialize(json);
        var note = () => TripJson.DeserializeNote(json);
        var photograph = () => TripJson.DeserializePhotograph(json);

        // Assert
        trip.Should().Throw<System.Text.Json.JsonException>();
        note.Should().Throw<System.Text.Json.JsonException>();
        photograph.Should().Throw<System.Text.Json.JsonException>();
    }
}
