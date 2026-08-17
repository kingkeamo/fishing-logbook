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
    public void ItShouldRoundTripCatchDtoLocationUsingWebDefaults()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var original = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, "image/jpeg")],
            new CatchLocationDto(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<CatchDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"latitude\":53.2707");
        deserialized.Should().BeEquivalentTo(original);
        deserialized!.Location.Should().NotBeNull();
        deserialized.Location!.Visibility.Should().Be(LocationDefaults.Private);
        deserialized.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        deserialized.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public void ItShouldRoundTripCatchDtoWithoutLocationUsingWebDefaults()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var original = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, "image/jpeg")]);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<CatchDto>(json, WebOptions);

        // Assert
        deserialized.Should().BeEquivalentTo(original);
        deserialized!.Location.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripProfileDtoWithoutPreciseCoordinates()
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
            false);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<ProfileDto>(json, WebOptions);

        // Assert
        json.Should().NotContain("latitude");
        json.Should().NotContain("longitude");
        json.Should().Contain("\"homeRegion\":\"Westmeath\"");
        typeof(ProfileDto).GetProperty("Location").Should().BeNull();
        typeof(ProfileDto).GetProperty("Latitude").Should().BeNull();
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldRoundTripPublicProfileDtoWithoutPreciseCoordinates()
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
        json.Should().NotContain("longitude");
        typeof(PublicProfileDto).GetProperty("Location").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Latitude").Should().BeNull();
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldRoundTripUpdateProfileDtoWithoutPreciseCoordinates()
    {
        // Arrange
        var original = new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<UpdateProfileDto>(json, WebOptions);

        // Assert
        json.Should().NotContain("latitude");
        json.Should().NotContain("userId");
        typeof(UpdateProfileDto).GetProperty("Location").Should().BeNull();
        deserialized.Should().BeEquivalentTo(original);
    }
}
