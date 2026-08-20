using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class WhenTestingDownloadPhotograph : BaseCatchClientTest
{
    [Fact]
    public async Task ItShouldDownloadTheBytesFromTheGivenUrl()
    {
        // Arrange
        var apiHandler = new RecordingHandler(HttpStatusCode.OK);
        var anonymousHandler = new RecordingHandler(HttpStatusCode.OK, "irrelevant");
        var client = CreateClient(apiHandler, anonymousHandler);

        // Act
        await client.DownloadPhotographAsync("https://r2.test/signed-download", CancellationToken.None);

        // Assert
        anonymousHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        anonymousHandler.LastRequest.RequestUri!.ToString().Should().Be("https://r2.test/signed-download");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheDownloadFails()
    {
        // Arrange
        var apiHandler = new RecordingHandler(HttpStatusCode.OK);
        var anonymousHandler = new RecordingHandler(HttpStatusCode.Forbidden);
        var client = CreateClient(apiHandler, anonymousHandler);

        // Act
        var action = () => client.DownloadPhotographAsync("https://r2.test/expired", CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
