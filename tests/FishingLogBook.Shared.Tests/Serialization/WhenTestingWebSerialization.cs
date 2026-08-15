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
}
