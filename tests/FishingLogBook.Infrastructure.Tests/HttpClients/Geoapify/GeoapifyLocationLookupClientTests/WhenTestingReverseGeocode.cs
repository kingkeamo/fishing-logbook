using System.Net;
using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.HttpClients.Geoapify.GeoapifyLocationLookupClientTests;

public class WhenTestingReverseGeocode : BaseGeoapifyLocationLookupClientTest
{
    [Fact]
    public async Task ItShouldFailWithoutCallingTheProviderWhenConfigurationIsMissing()
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{}")));
        var client = CreateClient(handler, new GeoapifyConfig());

        // Act
        var action = () => client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
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
    public async Task ItShouldMapTheProviderComponentsWithoutDiscardingDetail()
    {
        // Arrange
        const string body = """
            {
              "results": [
                {
                  "address_line1": "42 Street 7",
                  "suburb": "Zayed International Airport",
                  "district": "Arabian Village",
                  "city": "Abu Dhabi",
                  "state": "Abu Dhabi Emirate",
                  "country": "United Arab Emirates",
                  "formatted": "42 Street 7, Zayed International Airport, Abu Dhabi, Arabian Village, United Arab Emirates"
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
        result!.Components.Should().Equal(
            "42 Street 7",
            "Zayed International Airport",
            "Abu Dhabi",
            "Arabian Village",
            "United Arab Emirates");
        result.Street.Should().Be("42 Street 7");
        result.VenueOrFacility.Should().Be("Zayed International Airport");
        result.Locality.Should().Be("Abu Dhabi");
        result.DistrictOrNeighbourhood.Should().Be("Arabian Village");
        result.Region.Should().Be("Abu Dhabi Emirate");
        result.Country.Should().Be("United Arab Emirates");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be(
            "https://geoapify.test/v1/geocode/reverse?lat=53.3498&lon=-6.2603&format=json&apiKey=test-key");
    }

    [Fact]
    public async Task ItShouldFailForAnUnsuccessfulProviderResponse()
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{}", HttpStatusCode.TooManyRequests)));
        var client = CreateClient(handler);

        // Act
        var action = () => client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        handler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNormalizeBlankAndDuplicateComponents()
    {
        // Arrange
        const string body = """
            {
              "results": [
                {
                  "formatted": " Dublin , Dublin, , Ireland "
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
        result!.Components.Should().Equal("Dublin", "Ireland");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"formatted\":\" , , \"}")]
    [InlineData("{\"city\":\" \", \"country\":\" \"}")]
    public async Task ItShouldReturnNoResultWhenAllComponentsAreEmpty(string providerResult)
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{\"results\":[" + providerResult + "]}")));
        var client = CreateClient(handler);

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldUseOrderedFallbacksAndDeduplicateDisplayComponents()
    {
        // Arrange
        const string body = """
            {"results":[{
                "address_line1":" ",
                "street":" Harbour Road ",
                "name":"Marina",
                "suburb":"Other suburb",
                "district":"marina",
                "city":" ",
                "town":"Town",
                "village":"Village",
                "municipality":"Municipality",
                "county":"County",
                "state":" ",
                "country":" Country ",
                "formatted":" "
            }]}
            """;
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, body)));
        var client = CreateClient(handler);

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result!.Components.Should().Equal("Harbour Road", "Marina", "Town", "County", "Country");
        result.Street.Should().Be(" Harbour Road ");
        result.VenueOrFacility.Should().Be("Marina");
        result.Locality.Should().Be("Town");
        result.DistrictOrNeighbourhood.Should().Be("marina");
        result.Region.Should().Be("County");
        result.Country.Should().Be(" Country ");
        handler.InvocationCount.Should().Be(1);
    }

    [Theory]
    [InlineData("City", "Town", "Village", "Municipality", "City")]
    [InlineData(" ", "Town", "Village", "Municipality", "Town")]
    [InlineData(null, " ", "Village", "Municipality", "Village")]
    [InlineData(null, null, " ", "Municipality", "Municipality")]
    [InlineData(null, null, null, " ", "County")]
    public async Task ItShouldSelectTheFirstPopulatedLocality(
        string? city, string? town, string? village, string? municipality, string expectedLocality)
    {
        // Arrange
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            results = new[] { new { city, town, village, municipality, county = "County" } }
        });
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, body)));
        var client = CreateClient(handler);

        // Act
        var result = await client.ReverseGeocodeAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result!.Locality.Should().Be(expectedLocality);
        result.Region.Should().Be("County");
        result.DistrictOrNeighbourhood.Should().Be("County");
        result.Components.Should().Equal(
            new[] { expectedLocality, "County" }.Distinct(StringComparer.OrdinalIgnoreCase));
        handler.InvocationCount.Should().Be(1);
    }
}
