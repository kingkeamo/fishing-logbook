using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
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
                LocationDefaults.ConsentVersion))
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AnglerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RecordedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };

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
    public void ItShouldRoundTripCatchDtoDetailsUsingWebDefaults()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var original = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, "image/jpeg")])
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AnglerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RecordedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SpeciesName = "Pike",
            Weight = 2.5m,
            Length = 64m,
            Method = "Lure",
            BaitOrLure = "Spinner",
            Notes = "Weedline"
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<CatchDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"speciesName\":\"Pike\"");
        json.Should().Contain("\"weight\":2.5");
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldOmitNullExactCoordinatesFromCatchLocationExposureDto()
    {
        // Arrange
        var original = new CatchLocationExposureDto
        {
            Visibility = LocationDefaults.Private,
            Mode = LocationDefaults.ExposureNone
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<CatchLocationExposureDto>(json, WebOptions);

        // Assert
        json.Should().NotContain("\"latitude\":");
        json.Should().NotContain("\"longitude\":");
        json.Should().NotContain("53.2707");
        json.Should().Contain("\"mode\":\"None\"");
        deserialized.Should().NotBeNull();
        deserialized!.Latitude.Should().BeNull();
        deserialized.Mode.Should().Be(LocationDefaults.ExposureNone);
    }

    [Fact]
    public void ItShouldSerializeApproximateCoordinatesWithoutExactFields()
    {
        // Arrange
        var original = new CatchLocationExposureDto
        {
            Visibility = LocationDefaults.Approximate,
            Mode = LocationDefaults.ExposureApproximate,
            ApproximateLatitude = 53.275,
            ApproximateLongitude = -9.075,
            ApproximateCellSizeMetres = CatchLocationConstants.ApproximateCellSizeMetres
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<CatchLocationExposureDto>(json, WebOptions);

        // Assert
        json.Should().NotContain("\"latitude\":");
        json.Should().NotContain("\"longitude\":");
        json.Should().Contain("\"approximateLatitude\":53.275");
        json.Should().Contain("\"approximateLongitude\":-9.075");
        deserialized.Should().NotBeNull();
        deserialized!.ApproximateLatitude.Should().Be(53.275);
        deserialized.Latitude.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripCatchViewDtoUsingWebDefaults()
    {
        // Arrange
        var original = new CatchViewDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            new CatchLocationExposureDto
            {
                Visibility = LocationDefaults.Public,
                Mode = LocationDefaults.ExposureExact,
                Latitude = 53.2707,
                Longitude = -9.0568
            })
        {
            AnglerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RecordedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<CatchViewDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"latitude\":53.2707");
        deserialized.Should().BeEquivalentTo(original);
        deserialized!.Location!.Latitude.Should().Be(53.2707);
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
