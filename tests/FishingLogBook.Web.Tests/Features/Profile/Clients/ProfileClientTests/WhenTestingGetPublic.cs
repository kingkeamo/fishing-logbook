using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.ProfileClientTests;

public class WhenTestingGetPublic : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.NotFound);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/profiles/{userId:D}");
    }

    [Fact]
    public async Task ItShouldGetThePublicProfileForTheRequestedUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expected = new PublicProfileDto(userId, "Eamonn", null, "Westmeath", ["Fly"], ["Pike"]);
        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expected);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/profiles/{userId:D}");
    }
}
