using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.SystemStatus;

namespace FishingLogBook.Shared.Tests.Serialization;

public class WhenTestingWebSerialization : BaseSerializationTest
{
    [Fact]
    public void ItShouldRoundTripHealthResponseUsingWebDefaults()
    {
        // Arrange
        var original = new HealthResponse("Healthy");

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<HealthResponse>(json, WebOptions);

        // Assert
        json.Should().Contain("\"status\":\"Healthy\"");
        deserialized.Should().Be(original);
    }

    [Fact]
    public void ItShouldRoundTripDatabaseTestResponseUsingWebDefaults()
    {
        // Arrange
        var original = new DatabaseTestResponse("Healthy", "FishingLogBook database online");

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<DatabaseTestResponse>(json, WebOptions);

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
