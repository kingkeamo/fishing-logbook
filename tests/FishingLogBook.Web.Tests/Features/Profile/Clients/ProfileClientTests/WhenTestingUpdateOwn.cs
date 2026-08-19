using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.ProfileClientTests;

public class WhenTestingUpdateOwn : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.BadRequest);
        var client = CreateClient(apiHandler);
        var request = new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false);

        // Act
        var act = () => client.UpdateOwnAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me");
    }

    [Fact]
    public async Task ItShouldPutTheExactUpdateProfileDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false);
        var saved = OwnProfile(userId);
        var json = JsonSerializer.Serialize(saved, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.UpdateOwnAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(saved);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me");
        var sent = JsonSerializer.Deserialize<UpdateProfileDto>(
            apiHandler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().BeEquivalentTo(request);
        apiHandler.LastBody.Should().NotContain("latitude");
        apiHandler.LastBody.Should().NotContain("userId");
    }
}
