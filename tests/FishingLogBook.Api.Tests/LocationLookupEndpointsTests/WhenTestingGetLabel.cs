using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.LocationLookupEndpointsTests;

public class WhenTestingGetLabel : IClassFixture<SystemApiFactory>
{
    private const string ResourcePath = "/api/locations/label";
    private static readonly LocationLookupRequestDto Request = new(53.3498, -6.2603);
    private readonly SystemApiFactory _factory;

    public WhenTestingGetLabel(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.LocationLookupService.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync(ResourcePath, Request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.LocationLookupService.DidNotReceive().ReverseGeocodeAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectCoordinatesOutsideTheValidRange()
    {
        // Arrange
        _factory.LocationLookupService.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            ResourcePath,
            new LocationLookupRequestDto(91, -6.2603));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.LocationLookupService.DidNotReceive().ReverseGeocodeAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoContentWhenNoLabelWasResolved()
    {
        // Arrange
        _factory.LocationLookupService
            .ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<LocationLookupDto?>(null));
        _factory.LocationLookupService.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        using var response = await client.PostAsJsonAsync(ResourcePath, Request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.LocationLookupService.Received(1).ReverseGeocodeAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheResolvedLocationLabel()
    {
        // Arrange
        var expected = new LocationLookupDto(["Dublin", "Ireland"])
        {
            Locality = "Dublin",
            Country = "Ireland"
        };
        _factory.LocationLookupService
            .ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<LocationLookupDto?>(expected));
        _factory.LocationLookupService.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        using var response = await client.PostAsJsonAsync(ResourcePath, Request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var location = await response.Content.ReadFromJsonAsync<LocationLookupDto>();
        location.Should().BeEquivalentTo(expected);
        await _factory.LocationLookupService.Received(1).ReverseGeocodeAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenLookupFails()
    {
        // Arrange
        _factory.LocationLookupService
            .ReverseGeocodeAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<LocationLookupDto?>("Provider failed."));
        _factory.LocationLookupService.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        using var response = await client.PostAsJsonAsync(ResourcePath, Request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.LocationLookupService.Received(1).ReverseGeocodeAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }
}
