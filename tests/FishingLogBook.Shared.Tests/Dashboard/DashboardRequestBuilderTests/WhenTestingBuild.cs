using AwesomeAssertions;
using FishingLogBook.Shared.Dashboard.Builders;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Shared.Tests.Dashboard.DashboardRequestBuilderTests;

public class WhenTestingBuild
{
    [Fact]
    public void ItShouldOmitAllUnboundedFilters()
    {
        // Arrange
        var filter = new DashboardFilterDto();

        // Act
        var uri = DashboardRequestBuilder.Build(filter);

        // Assert
        uri.Should().Be("api/dashboard");
    }

    [Fact]
    public void ItShouldEscapeEachFilterWithoutAddingAnIdentity()
    {
        // Arrange
        var filter = new DashboardFilterDto
        {
            FromInclusive = DateTimeOffset.Parse("2026-01-01T00:00:00+04:00"),
            ToExclusive = DateTimeOffset.Parse("2027-01-01T00:00:00+04:00"),
            Species = "Sea & trout",
            Method = "Fly/float",
            Venue = "Lac d’Été?x=1"
        };

        // Act
        var uri = DashboardRequestBuilder.Build(filter);

        // Assert
        uri.Should().Be("api/dashboard?fromInclusive=2026-01-01T00%3A00%3A00.0000000%2B04%3A00&toExclusive=2027-01-01T00%3A00%3A00.0000000%2B04%3A00&species=Sea%20%26%20trout&method=Fly%2Ffloat&venue=Lac%20d%E2%80%99%C3%89t%C3%A9%3Fx%3D1");
    }
}
