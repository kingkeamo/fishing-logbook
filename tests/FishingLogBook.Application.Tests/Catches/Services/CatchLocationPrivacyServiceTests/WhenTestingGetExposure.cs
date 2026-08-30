using AwesomeAssertions;
using FishingLogBook.Application.Capabilities.Contracts.Services;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchLocationPrivacyServiceTests;

public class WhenTestingGetExposure : BaseCatchLocationPrivacyServiceTest
{
    [Fact]
    public async Task ItShouldReturnNullWhenTheCatchHasNoLocation()
    {
        // Arrange
        var catchRecord = new FishingLogBook.Domain.Catches.Catch
        {
            Id = Guid.NewGuid(),
            UserId = OwnerUserId,
            CaughtOn = DateTimeOffset.UtcNow
        };

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().BeNull();
    }

    [Theory]
    [InlineData(LocationDefaults.Private)]
    [InlineData(LocationDefaults.Approximate)]
    [InlineData(LocationDefaults.FishingVenueOnly)]
    [InlineData(LocationDefaults.Public)]
    public async Task ItShouldReturnExactCoordinatesForTheOwnerAtEveryVisibility(string visibility)
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, visibility);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, OwnerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.Mode.Should().Be(LocationDefaults.ExposureExact);
        exposure.Latitude.Should().Be(53.2707);
        exposure.Longitude.Should().Be(-9.0568);
        exposure.ApproximateLatitude.Should().BeNull();
        exposure.ApproximateLongitude.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldHideExactCoordinatesFromAnotherUserWhenPrivate()
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, LocationDefaults.Private);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.Mode.Should().Be(LocationDefaults.ExposureNone);
        exposure.Latitude.Should().BeNull();
        exposure.Longitude.Should().BeNull();
        exposure.ApproximateLatitude.Should().BeNull();
        exposure.FishingVenueId.Should().BeNull();
        exposure.FishingVenueName.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnOnlyGeneralisedCoordinatesWhenApproximate()
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, LocationDefaults.Approximate);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.Mode.Should().Be(LocationDefaults.ExposureApproximate);
        exposure.Latitude.Should().BeNull();
        exposure.Longitude.Should().BeNull();
        exposure.AccuracyMetres.Should().BeNull();
        exposure.ApproximateLatitude.Should().Be(53.275);
        exposure.ApproximateLongitude.Should().Be(-9.075);
        exposure.ApproximateCellSizeMetres.Should().Be(CatchLocationConstants.ApproximateCellSizeMetres);
        exposure.ApproximateLatitude.Should().NotBe(53.2707);
        exposure.ApproximateLongitude.Should().NotBe(-9.0568);
    }

    [Fact]
    public async Task ItShouldNotExposeGpsOrInferAVenueWhenFishingVenueOnly()
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, LocationDefaults.FishingVenueOnly);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.Mode.Should().Be(LocationDefaults.ExposureFishingVenue);
        exposure.Latitude.Should().BeNull();
        exposure.Longitude.Should().BeNull();
        exposure.ApproximateLatitude.Should().BeNull();
        exposure.FishingVenueId.Should().BeNull();
        exposure.FishingVenueName.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnExactCoordinatesToAnotherUserWhenPublic()
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, LocationDefaults.Public);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.Mode.Should().Be(LocationDefaults.ExposureExact);
        exposure.Latitude.Should().Be(53.2707);
        exposure.Longitude.Should().Be(-9.0568);
    }

    [Fact]
    public async Task ItShouldClampApproximateLatitudeAtThePole()
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, LocationDefaults.Approximate, latitude: 90, longitude: 0);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.ApproximateLatitude.Should().Be(90);
        exposure.Latitude.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldWrapApproximateLongitudeAcrossTheDateLine()
    {
        // Arrange
        var catchRecord = LocatedCatch(OwnerUserId, LocationDefaults.Approximate, latitude: 0, longitude: 180);

        // Act
        var exposure = await Sut.GetExposureAsync(catchRecord, ViewerUserId, CancellationToken.None);

        // Assert
        exposure.Should().NotBeNull();
        exposure!.ApproximateLongitude.Should().Be(-179.975);
        exposure.Longitude.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotConsultPlatformCapabilities()
    {
        // Arrange
        var constructor = typeof(CatchLocationPrivacyService).GetConstructors().Single();

        // Act
        var exposure = await Sut.GetExposureAsync(
            LocatedCatch(OwnerUserId),
            ViewerUserId,
            CancellationToken.None);

        // Assert
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should()
            .NotContain(typeof(IPlatformCapabilityService));
        Enum.GetNames<PlatformCapabilityEnum>().Should().Equal(
            nameof(PlatformCapabilityEnum.Guide),
            nameof(PlatformCapabilityEnum.FishingVenueManager),
            nameof(PlatformCapabilityEnum.CompetitionOrganiser),
            nameof(PlatformCapabilityEnum.Administrator));
        exposure!.Latitude.Should().BeNull();
    }
}
