using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public class WhenTestingGetCatalogue : BaseFishingPreferenceClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsAFailureStatus()
    {
        // Arrange
        var handler = new RecordingHandler("""{"detail":"unavailable"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetCatalogueAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/fishing-catalogue");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsNoCatalogue()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetCatalogueAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Be("Fishing catalogue was missing.");
    }

    [Fact]
    public async Task ItShouldRequestTheCatalogueAndDeserialiseBothLists()
    {
        // Arrange
        var body = $$"""
            {
              "methods": [ { "id": "{{FlyMethodId}}", "code": "Fly", "name": "Fly" } ],
              "allSpecies": [ { "id": "{{BrownTroutSpeciesId}}", "code": "BrownTrout", "name": "Brown Trout" } ]
            }
            """;
        var handler = new RecordingHandler(body);
        var client = CreateClient(handler);

        // Act
        var catalogue = await client.GetCatalogueAsync(CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/fishing-catalogue");
        catalogue.Methods.Should().ContainSingle(method => method.Id == FlyMethodId && method.Name == "Fly");
        catalogue.AllSpecies.Should().ContainSingle(species => species.Id == BrownTroutSpeciesId);
    }
}
