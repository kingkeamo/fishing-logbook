using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Shared.Tests.Serialization;

public class WhenTestingWebSerialization : BaseSerializationTest
{
    [Fact]
    public void ItShouldRoundTripHealthDtoUsingWebDefaults()
    {
        // Arrange
        var original = new HealthDto("Healthy");

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<HealthDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"status\":\"Healthy\"");
        deserialized.Should().Be(original);
    }

    [Fact]
    public void ItShouldRoundTripDatabaseTestDtoUsingWebDefaults()
    {
        // Arrange
        var original = new DatabaseTestDto("Healthy", "FishingLogBook database online");

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<DatabaseTestDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"name\":\"FishingLogBook database online\"");
        deserialized.Should().Be(original);
    }

    [Fact]
    public void ItShouldRoundTripTestRecordDtoUsingWebDefaults()
    {
        // Arrange
        var original = new TestRecordDto(Guid.NewGuid(), "Sample", DateTimeOffset.UtcNow);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<TestRecordDto>(json, WebOptions);

        // Assert
        deserialized.Should().Be(original);
    }

    [Fact]
    public void ItShouldRoundTripTestCatchDtoLocationUsingWebDefaults()
    {
        // Arrange
        var original = new TestCatchDto(
            Guid.NewGuid(),
            "Pike",
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            null,
            Location: new CatchLocationDto(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<TestCatchDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"latitude\":53.2707");
        deserialized.Should().Be(original);
        deserialized!.Location.Should().NotBeNull();
        deserialized.Location!.Visibility.Should().Be(LocationDefaults.Private);
    }

    [Fact]
    public void ItShouldRoundTripProfileDtoLocationUsingWebDefaults()
    {
        // Arrange
        var original = new ProfileDto(
            Guid.NewGuid(),
            "Eamonn",
            null,
            null,
            null,
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false,
            new CatchLocationDto(
                53.4,
                -7.9,
                12,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Public,
                LocationDefaults.ConsentVersion));

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<ProfileDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"latitude\":53.4");
        json.Should().Contain("\"visibility\":\"Public\"");
        deserialized.Should().NotBeNull();
        deserialized!.DisplayName.Should().Be("Eamonn");
        deserialized.HomeRegion.Should().Be("Westmeath");
        deserialized.PreferredFishingTypes.Should().Equal("Coarse");
        deserialized.Location.Should().NotBeNull();
        deserialized.Location!.Visibility.Should().Be(LocationDefaults.Public);
        deserialized.Location.Latitude.Should().Be(53.4);
    }

    [Fact]
    public void ItShouldRoundTripPublicProfileDtoWithoutCoordinatesWhenLocationIsOmitted()
    {
        // Arrange
        var original = new PublicProfileDto(
            Guid.NewGuid(),
            "Eamonn",
            null,
            "Westmeath",
            ["Fly"],
            ["Pike"]);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<PublicProfileDto>(json, WebOptions);

        // Assert
        json.Should().NotContain("latitude");
        deserialized.Should().NotBeNull();
        deserialized!.DisplayName.Should().Be("Eamonn");
        deserialized.HomeRegion.Should().Be("Westmeath");
        deserialized.PreferredFishingTypes.Should().Equal("Fly");
        deserialized.Location.Should().BeNull();
    }
}
