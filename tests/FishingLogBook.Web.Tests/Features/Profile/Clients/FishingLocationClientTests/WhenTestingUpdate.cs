using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.FishingLocationClientTests;

public class WhenTestingUpdate : BaseFishingLocationClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiRejectsTheLocations()
    {
        // Arrange
        var handler = new RecordingHandler("""{"errorMessage":"bad"}""", HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        // Act
        var act = () => client.UpdateAsync(ValidUpdate(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be("https://api.test/api/profiles/me/fishing-locations");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsNoSavedLocations()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.UpdateAsync(ValidUpdate(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Be("Saved fishing locations were missing.");
    }

    [Fact]
    public async Task ItShouldPutTheLocationsAndReturnWhatWasSaved()
    {
        // Arrange
        var body = $$"""
            {
              "locations": [
                { "id": "{{CorribId}}", "name": "Lough Corrib", "isDefault": true }
              ]
            }
            """;
        var handler = new RecordingHandler(body);
        var client = CreateClient(handler);

        // Act
        var saved = await client.UpdateAsync(ValidUpdate(), CancellationToken.None);

        // Assert
        saved.Locations.Single().Id.Should().Be(CorribId);
        saved.Locations.Single().IsDefault.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be("https://api.test/api/profiles/me/fishing-locations");
        handler.LastBody.Should().Contain("\"name\":\"Lough Corrib\"");
        handler.LastBody.Should().Contain("\"isDefault\":true");
    }

    private static UpdateFishingLocationPreferencesDto ValidUpdate()
    {
        return new UpdateFishingLocationPreferencesDto(
            [new UpdateFishingLocationPreferenceDto(CorribId, "Lough Corrib", true)]);
    }
}
