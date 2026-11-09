using AwesomeAssertions;
using FishingLogBook.Application.LocationLookup.Contracts.Services;
using FishingLogBook.Application.LocationLookup.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.LocationLookup.Queries.GetLocationLabelQueryTests;

public class WhenTestingHandle
{
    private readonly ILocationLookupService _mockService = Substitute.For<ILocationLookupService>();
    private readonly GetLocationLabelHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new GetLocationLabelHandler(_mockService);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<LocationLookupDto?>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new GetLocationLabelQuery { Latitude = 53.3498, Longitude = -6.2603 },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Location.Should().BeNull();
        await _mockService.Received(1).ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSucceedWithNoLocationWhenNothingMatches()
    {
        // Arrange
        _mockService.ReverseGeocodeAsync(0d, 0d, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<LocationLookupDto?>(null));

        // Act
        var response = await _sut.Handle(new GetLocationLabelQuery(), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Location.Should().BeNull();
        await _mockService.Received(1).ReverseGeocodeAsync(0d, 0d, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheLabelForTheCoordinates()
    {
        // Arrange
        var label = new LocationLookupDto(["Dublin", "Ireland"]) { Locality = "Dublin" };
        _mockService.ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<LocationLookupDto?>(label));

        // Act
        var response = await _sut.Handle(
            new GetLocationLabelQuery { Latitude = 53.3498, Longitude = -6.2603 },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Location.Should().Be(label);
        await _mockService.Received(1).ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>());
    }
}
