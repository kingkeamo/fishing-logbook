using System.Net;
using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.HttpClients.Geoapify.GeoapifyLocationLookupClientTests;

public class WhenTestingReverseGeocode : BaseGeoapifyLocationLookupClientTest
{
    [Fact]
    public async Task ItShouldSkipTheProviderWhenConfigurationIsMissing()
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{}")));
        var client = CreateClient(handler, new GeoapifyConfig());

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldReturnNoResultWhenTheProviderHasNoMatch()
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, """{"results":[]}""")));
        var client = CreateClient(handler);

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldMapTheBestAvailableShortPlaceLabel()
    {
        // Arrange
        const string body = """
            {
              "results": [
                {
                  "city": "Dublin",
                  "county": "County Dublin",
                  "state": "Leinster",
                  "country": "Ireland"
                }
              ]
            }
            """;
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, body)));
        var client = CreateClient(handler);

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result!.DisplayName.Should().Be("Dublin, Leinster, Ireland");
        result.Locality.Should().Be("Dublin");
        result.Region.Should().Be("Leinster");
        result.Country.Should().Be("Ireland");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be(
            "https://geoapify.test/v1/geocode/reverse?lat=53.3498&lon=-6.2603&format=json&apiKey=test-key");
    }

    [Fact]
    public async Task ItShouldReturnNoResultForAnUnsuccessfulProviderResponse()
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{}", HttpStatusCode.TooManyRequests)));
        var client = CreateClient(handler);

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.InvocationCount.Should().Be(1);
    }
}
