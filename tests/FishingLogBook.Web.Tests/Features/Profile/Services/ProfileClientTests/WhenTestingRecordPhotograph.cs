using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.ProfileClientTests;

public class WhenTestingRecordPhotograph : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.BadRequest);
        var client = CreateClient(apiHandler);
        var request = new RecordPhotographDto(Guid.NewGuid(), "profiles/key", PhotographContentTypeConstants.Jpeg);

        // Act
        var act = () => client.RecordPhotographAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/photograph");
    }

    [Fact]
    public async Task ItShouldPostTheExactRecordRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new RecordPhotographDto(
            Guid.NewGuid(),
            "profiles/user/photo",
            PhotographContentTypeConstants.Webp);
        var saved = OwnProfile(userId);
        var json = JsonSerializer.Serialize(saved, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.RecordPhotographAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(saved);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/photograph");
        var sent = JsonSerializer.Deserialize<RecordPhotographDto>(
            apiHandler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().BeEquivalentTo(request);
    }
}
