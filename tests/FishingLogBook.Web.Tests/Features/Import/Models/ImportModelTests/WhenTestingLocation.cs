using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingLocation : BaseImportModelTest
{
    [Fact]
    public void ItShouldNotTreatAPlaceLabelAsCanonicalCoordinates()
    {
        // Arrange
        var location = new ImportLocationModel(null, null, false)
            .WithLookup(
                ImportLocationLookupStatusEnum.Resolved,
                new ImportLocationLookupResultModel(["Galway", "Ireland"])
                {
                    Locality = "Galway",
                    Country = "Ireland"
                });

        // Act
        var hasCoordinates = location.HasCanonicalCoordinates;

        // Assert
        hasCoordinates.Should().BeFalse();
        location.LookupResult!.DisplayName.Should().Be("Galway, Ireland");
        location.Invoking(candidate => candidate.Accept()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldAcceptAndRemoveHistoricalCoordinatesWithoutChangingThem()
    {
        // Arrange
        var location = new ImportLocationModel(53.3498, -6.2603, true);

        // Act
        var accepted = location.Accept();
        var removed = accepted.Remove();

        // Assert
        accepted.Decision.Should().Be(ImportLocationDecisionEnum.Accepted);
        removed.Decision.Should().Be(ImportLocationDecisionEnum.Removed);
        removed.Latitude.Should().Be(53.3498);
        removed.Longitude.Should().Be(-6.2603);
    }

    [Fact]
    public void ItShouldDefaultToTheCompleteResolvedPlaceNameAndClearItWhenRemoved()
    {
        // Arrange
        var lookup = new ImportLocationLookupResultModel(["Street 7", "Abu Dhabi", "United Arab Emirates"]);
        var location = new ImportLocationModel(24.4576, 54.6702, true)
            .WithLookup(ImportLocationLookupStatusEnum.Resolved, lookup);

        // Act
        var removed = location.Remove();

        // Assert
        location.PlaceName.Should().Be("Street 7, Abu Dhabi, United Arab Emirates");
        removed.PlaceName.Should().BeNull();
    }

    [Fact]
    public void ItShouldRejectPartialCoordinates()
    {
        // Arrange
        Action create = () => _ = new ImportLocationModel(53.3498, null, true);

        // Act
        var assertion = create.Should();

        // Assert
        assertion.Throw<ArgumentException>();
    }
}
