using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.ProfileClientTests;

public class WhenTestingCreatePhotographUpload : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(apiHandler);
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg);

        // Act
        var act = () => client.CreatePhotographUploadAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/photograph/upload-url");
    }

    [Fact]
    public async Task ItShouldPostTheExactUploadRequest()
    {
        // Arrange
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Png);
        var expected = new PhotographUploadDto("profiles/user/photo", "https://storage.test/upload");
        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.CreatePhotographUploadAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expected);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/photograph/upload-url");
        var sent = JsonSerializer.Deserialize<PhotographUploadRequestDto>(
            apiHandler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().BeEquivalentTo(request);
    }
}
