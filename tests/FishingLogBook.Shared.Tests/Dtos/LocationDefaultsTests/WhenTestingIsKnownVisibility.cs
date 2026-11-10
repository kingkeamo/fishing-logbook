using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Shared.Tests.Dtos.LocationDefaultsTests;

public class WhenTestingIsKnownVisibility
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("private")]
    [InlineData("Everyone")]
    [InlineData("None")]
    [InlineData("Exact")]
    public void ItShouldRejectAValueThatIsNotAnExactlyCasedVisibility(string? visibility)
    {
        // Arrange
        // Act
        var result = LocationDefaults.IsKnownVisibility(visibility);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(LocationDefaults.Private)]
    [InlineData(LocationDefaults.Approximate)]
    [InlineData(LocationDefaults.FishingVenueOnly)]
    [InlineData(LocationDefaults.Public)]
    public void ItShouldAcceptEverySupportedVisibility(string visibility)
    {
        // Arrange
        // Act
        var result = LocationDefaults.IsKnownVisibility(visibility);

        // Assert
        result.Should().BeTrue();
    }
}
