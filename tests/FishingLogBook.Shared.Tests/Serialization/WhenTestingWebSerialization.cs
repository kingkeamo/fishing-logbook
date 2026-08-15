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
}
