using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.ProfileClientTests;

public class WhenTestingGetOwn : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.GetOwnAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me");
    }

    [Fact]
    public async Task ItShouldGetTheOwnProfileFromTheAuthorizedApi()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expected = OwnProfile(userId);
        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var anonymousHandler = new RecordingHandler("""ok""");
        var client = CreateClient(apiHandler, anonymousHandler);

        // Act
        var result = await client.GetOwnAsync(CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expected);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me");
        anonymousHandler.LastRequest.Should().BeNull();
    }
}
