using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.LocationLookup.Services.LocationLookupServiceTests;

public class WhenTestingReverseGeocode : BaseLocationLookupServiceTest
{
    [Fact]
    public async Task ItShouldReturnNoResultWithoutFailingWhenTheProviderHasNoMatch()
    {
        // Arrange
        var (service, client, _) = CreateService();
        client.ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns((LocationLookupDto?)null);

        // Act
        var result = await service.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        await client.Received(1).ReverseGeocodeAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoResultAndAvoidLoggingCoordinatesWhenTheProviderFails()
    {
        // Arrange
        var (service, client, logger) = CreateService();
        client.ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns<Task<LocationLookupDto?>>(_ => throw new HttpRequestException("Provider unavailable."));

        // Act
        var result = await service.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        logger.Messages.Should().ContainSingle();
        logger.Messages.Single().Should().NotContain("53.3498").And.NotContain("-6.2603");
        await client.Received(1).ReverseGeocodeAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRoundOnlyTheLookupAndCacheCoordinates()
    {
        // Arrange
        var (service, client, _) = CreateService();
        var expected = new LocationLookupDto("Dublin, Ireland", "Dublin", null, "Ireland");
        client.ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var result = await service.ReverseGeocodeAsync(53.3498123, -6.2603123, CancellationToken.None);

        // Assert
        result.Value.Should().Be(expected);
        await client.Received(1).ReverseGeocodeAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseOneProviderCallForEquivalentRoundedCoordinates()
    {
        // Arrange
        var (service, client, _) = CreateService();
        var expected = new LocationLookupDto("Dublin, Ireland", "Dublin", null, "Ireland");
        client.ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var first = await service.ReverseGeocodeAsync(53.34981, -6.26031, CancellationToken.None);
        var second = await service.ReverseGeocodeAsync(53.34982, -6.26032, CancellationToken.None);

        // Assert
        first.Value.Should().Be(expected);
        second.Value.Should().Be(expected);
        await client.Received(1).ReverseGeocodeAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }
}
