using AwesomeAssertions;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Infrastructure.Tests.Persistence.Mappings.CatchMappingRegistrationTests;

public class WhenTestingMap : BaseCatchMappingRegistrationTest
{
    [Fact]
    public void ItShouldNotMapALocationWhenLatitudeIsMissing()
    {
        // Arrange
        var row = NewRow(latitude: null, longitude: -9.0568);

        // Act
        var result = Mapper.Map<Catch>(row);

        // Assert
        result.Location.Should().BeNull();
    }

    [Fact]
    public void ItShouldNotMapALocationWhenLongitudeIsMissing()
    {
        // Arrange
        var row = NewRow(latitude: 53.2707, longitude: null);

        // Act
        var result = Mapper.Map<Catch>(row);

        // Assert
        result.Location.Should().BeNull();
    }

    [Fact]
    public void ItShouldNotMapALocationWhenTheLocationDataFailsValidation()
    {
        // Arrange
        var row = NewRow(latitude: 53.2707, longitude: -9.0568, locationConsentVersion: null);

        // Act
        var result = Mapper.Map<Catch>(row);

        // Assert
        result.Location.Should().BeNull();
    }

    [Fact]
    public void ItShouldMapALocationWhenTheRowHasValidCoordinates()
    {
        // Arrange
        var capturedOn = DateTimeOffset.Parse("2026-08-17T09:15:00Z");
        var row = NewRow(
            latitude: 53.2707,
            longitude: -9.0568,
            accuracyMetres: 12,
            locationCapturedOn: capturedOn,
            locationSource: LocationDefaults.DeviceGps,
            locationVisibility: LocationDefaults.Private,
            locationConsentVersion: LocationDefaults.ConsentVersion);

        // Act
        var result = Mapper.Map<Catch>(row);

        // Assert
        result.Location.Should().NotBeNull();
        result.Location!.Latitude.Should().Be(53.2707);
        result.Location.Longitude.Should().Be(-9.0568);
        result.Location.AccuracyMetres.Should().Be(12);
        result.Location.CapturedOn.Should().Be(capturedOn);
        result.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        result.Location.Visibility.Should().Be(LocationDefaults.Private);
        result.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }
}
