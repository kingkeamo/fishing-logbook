using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public sealed class WhenTestingUpdatePreferences : BaseFishingPreferenceClientTest
{
    [Fact]
    public async Task ItShouldReplaceTheCompletePreferenceListThroughTheExistingEndpoint()
    {
        // Arrange
        var update = new UpdateFishingPreferencesDto([]);
        var (sut, server) = CreateClient(_ => Json("{\"methods\":[]}"));

        // Act
        var result = await sut.UpdatePreferencesAsync(update, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/profiles/me/fishing-preferences"));
    }
}
