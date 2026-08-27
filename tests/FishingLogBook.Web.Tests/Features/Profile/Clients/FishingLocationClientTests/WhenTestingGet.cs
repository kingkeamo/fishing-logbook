using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.FishingLocationClientTests;

public class WhenTestingGet : BaseFishingLocationClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiFails()
    {
        // Arrange
        var handler = new RecordingHandler("""{"errorMessage":"bad"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be("https://api.test/api/profiles/me/fishing-locations");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsNoLocationsPayload()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Be("Fishing locations were missing.");
    }

    [Fact]
    public async Task ItShouldReadAnEmptyList()
    {
        // Arrange
        var handler = new RecordingHandler("""{"locations":[]}""");
        var client = CreateClient(handler);

        // Act
        var locations = await client.GetAsync(CancellationToken.None);

        // Assert
        locations.Locations.Should().BeEmpty();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should()
            .Be("https://api.test/api/profiles/me/fishing-locations");
    }

    [Fact]
    public async Task ItShouldReadTheSavedLocationsAndTheDefault()
    {
        // Arrange
        var body = $$"""
            {
              "locations": [
                { "id": "{{CorribId}}", "name": "Lough Corrib", "isDefault": true },
                { "id": "{{MoyId}}", "name": "River Moy", "isDefault": false }
              ]
            }
            """;
        var handler = new RecordingHandler(body);
        var client = CreateClient(handler);

        // Act
        var locations = await client.GetAsync(CancellationToken.None);

        // Assert
        locations.Locations.Should().HaveCount(2);
        locations.Locations[0].Id.Should().Be(CorribId);
        locations.Locations[0].Name.Should().Be("Lough Corrib");
        locations.Locations[0].IsDefault.Should().BeTrue();
        locations.Locations[1].IsDefault.Should().BeFalse();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
    }
}
