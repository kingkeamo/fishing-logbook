using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.ProfileClientTests;

public class WhenTestingUploadPhotograph : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenThePresignedUploadFails()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""ok""");
        var anonymousHandler = new RecordingHandler("""error""", HttpStatusCode.Forbidden);
        var client = CreateClient(apiHandler, anonymousHandler);
        var uploadUrl = "https://storage.example.test/photos/upload";

        // Act
        var act = () => client.UploadPhotographAsync(
            uploadUrl,
            [1, 2, 3],
            "image/jpeg",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().BeNull();
        anonymousHandler.LastRequest.Should().NotBeNull();
        anonymousHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        anonymousHandler.LastRequest.RequestUri.Should().Be(new Uri(uploadUrl));
    }

    [Fact]
    public async Task ItShouldPutTheExactBytesToThePresignedUrlWithoutABearerToken()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""ok""");
        var anonymousHandler = new RecordingHandler("""ok""");
        var client = CreateClient(apiHandler, anonymousHandler);
        var uploadUrl = "https://storage.example.test/photos/upload";
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF };

        // Act
        await client.UploadPhotographAsync(uploadUrl, bytes, "image/webp", CancellationToken.None);

        // Assert
        apiHandler.LastRequest.Should().BeNull();
        anonymousHandler.LastRequest.Should().NotBeNull();
        anonymousHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        anonymousHandler.LastRequest.RequestUri.Should().Be(new Uri(uploadUrl));
        anonymousHandler.LastRequest.Headers.Authorization.Should().BeNull();
        anonymousHandler.LastRequest.Content!.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("image/webp"));
        anonymousHandler.LastBytes.Should().Equal(bytes);
    }
}
