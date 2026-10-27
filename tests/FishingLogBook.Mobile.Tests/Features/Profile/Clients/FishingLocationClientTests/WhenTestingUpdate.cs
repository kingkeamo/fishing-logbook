using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingLocationClientTests;

public sealed class WhenTestingUpdate : BaseFishingLocationClientTest
{
    [Fact]
    public async Task ItShouldReplaceTheCompleteLocationListThroughTheExistingEndpoint()
    {
        // Arrange
        var update = new UpdateFishingLocationPreferencesDto([]);
        var (sut, server) = CreateClient(_ => Json("{\"locations\":[]}"));

        // Act
        var result = await sut.UpdateAsync(update, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/profiles/me/fishing-locations"));
    }
}
