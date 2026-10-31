using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Clients.PhotographDownloadClientTests;

public sealed class WhenTestingDownload
{
    [Theory]
    [InlineData("http://photos.example/image")]
    [InlineData("file:///private/image")]
    [InlineData("invalid")]
    public async Task ItShouldRejectUntrustedDownloadSchemesBeforeSending(string url)
    {
        // Arrange
        var factory = Substitute.For<IHttpClientFactory>();
        var sut = new PhotographDownloadClient(factory, NullLogger<PhotographDownloadClient>.Instance);
        // Act
        var result = await sut.DownloadAsync(url, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.Rejected);
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, PhotographDownloadOutcomeEnum.Downloaded)]
    [InlineData(HttpStatusCode.Forbidden, PhotographDownloadOutcomeEnum.Rejected)]
    [InlineData(HttpStatusCode.ServiceUnavailable, PhotographDownloadOutcomeEnum.Unavailable)]
    public async Task ItShouldDownloadFromStorageWithoutAnApiBearerToken(HttpStatusCode status, PhotographDownloadOutcomeEnum expected)
    {
        // Arrange
        var server = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent([1, 2, 3]) }));
        using var http = new HttpClient(server);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AuthenticationHttpClientConstants.Anonymous).Returns(http);
        var sut = new PhotographDownloadClient(factory, NullLogger<PhotographDownloadClient>.Instance);
        // Act
        var result = await sut.DownloadAsync("https://photos.example/image?signature=test", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(expected);
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Authorization.Should().BeNull();
        request.Uri!.AbsoluteUri.Should().Be("https://photos.example/image?signature=test");
        factory.Received(1).CreateClient(AuthenticationHttpClientConstants.Anonymous);
        if (status == HttpStatusCode.OK)
        {
            result.Bytes.Should().Equal([1, 2, 3]);
        }
    }
}
