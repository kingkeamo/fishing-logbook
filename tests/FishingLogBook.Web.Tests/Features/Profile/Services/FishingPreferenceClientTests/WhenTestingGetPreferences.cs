using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.FishingPreferenceClientTests;

public class WhenTestingGetPreferences : BaseFishingPreferenceClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsAFailureStatus()
    {
        // Arrange
        var handler = new RecordingHandler("""{"detail":"unavailable"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetPreferencesAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should()
            .Be("https://api.test/api/profiles/me/fishing-preferences");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsNoPreferences()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetPreferencesAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Be("Fishing preferences were missing.");
    }

    [Fact]
    public async Task ItShouldRequestThePreferencesAndDeserialiseTheNestedSpecies()
    {
        // Arrange
        var body = $$"""
            {
              "methods": [
                {
                  "fishingMethodId": "{{FlyMethodId}}",
                  "code": "Fly",
                  "name": "Fly",
                  "isDefault": true,
                  "species": [
                    {
                      "speciesId": "{{BrownTroutSpeciesId}}",
                      "code": "BrownTrout",
                      "name": "Brown Trout",
                      "isDefault": true
                    }
                  ]
                }
              ]
            }
            """;
        var handler = new RecordingHandler(body);
        var client = CreateClient(handler);

        // Act
        var preferences = await client.GetPreferencesAsync(CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        preferences.Methods.Should().ContainSingle();
        preferences.Methods[0].IsDefault.Should().BeTrue();
        preferences.Methods[0].Species[0].Name.Should().Be("Brown Trout");
        preferences.Methods[0].Species[0].IsDefault.Should().BeTrue();
    }
}
