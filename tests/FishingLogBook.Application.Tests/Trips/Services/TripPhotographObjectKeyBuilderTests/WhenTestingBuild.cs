using AwesomeAssertions;
using FishingLogBook.Application.Trips.Services;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographObjectKeyBuilderTests;

public class WhenTestingBuild
{
    [Fact]
    public void ItShouldReturnTheCanonicalTripPhotographKey()
    {
        // Arrange
        var sut = new TripPhotographObjectKeyBuilder();
        var tripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var objectKey = sut.Build(tripId, photographId);

        // Assert
        objectKey.Should().Be($"trip-photographs/{tripId:D}/{photographId:D}");
    }
}
