using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public class WhenTestingUpdatePreferences : BaseFishingPreferenceClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiRejectsTheSelection()
    {
        // Arrange
        var handler = new RecordingHandler("""{"errorMessage":"bad"}""", HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        // Act
        var act = () => client.UpdatePreferencesAsync(ValidUpdate(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be("https://api.test/api/profiles/me/fishing-preferences");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheApiReturnsNoSavedPreferences()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.UpdatePreferencesAsync(ValidUpdate(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Be("Saved fishing preferences were missing.");
    }

    [Fact]
    public async Task ItShouldPutTheSelectionAndReturnTheSavedPreferences()
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
                  "species": []
                }
              ]
            }
            """;
        var handler = new RecordingHandler(body);
        var client = CreateClient(handler);

        // Act
        var saved = await client.UpdatePreferencesAsync(ValidUpdate(), CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastBody.Should().Contain($"\"fishingMethodId\":\"{FlyMethodId}\"");
        handler.LastBody.Should().Contain($"\"speciesId\":\"{BrownTroutSpeciesId}\"");
        handler.LastBody.Should().Contain("\"isDefault\":true");
        saved.Methods.Should().ContainSingle(method => method.FishingMethodId == FlyMethodId);
    }

    private static UpdateFishingPreferencesDto ValidUpdate()
    {
        return new UpdateFishingPreferencesDto(
        [
            new UpdateFishingMethodPreferenceDto(
                FlyMethodId,
                true,
                [new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, true)])
        ]);
    }
}
