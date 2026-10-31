using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Clients.PhotographUploadClientTests;

public sealed class WhenTestingUpload : BasePhotographUploadClientTest
{
    [Fact]
    public async Task ItShouldRejectNullContent()
    {
        // Arrange
        CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var action = () => Sut.UploadAsync(ValidUrl, null!, ValidContentType, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
        Server.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://storage.example/catches/c/p?sig=1")]
    [InlineData("/catches/c/p?sig=1")]
    [InlineData("catches/c/p")]
    [InlineData("ftp://storage.example/catches/c/p")]
    [InlineData("")]
    public async Task ItShouldRejectAnUnsafeUploadUrlWithoutSendingARequest(string uploadUrl)
    {
        // Arrange
        CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(uploadUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.UrlRejected);
        Server.Requests.Should().BeEmpty();
        MockHttpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a media type")]
    [InlineData("image")]
    public async Task ItShouldRejectAnInvalidContentTypeWithoutSendingARequest(string contentType)
    {
        // Arrange
        CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, contentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.UrlRejected);
        Server.Requests.Should().BeEmpty();
        MockHttpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ItShouldReportTheUrlAsRejectedWhenStorageRefusesIt(HttpStatusCode status)
    {
        // Arrange
        CreateSut(_ => new HttpResponseMessage(status));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.UrlRejected);
        Server.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ItShouldReportUnavailableWhenStorageFails(HttpStatusCode status)
    {
        // Arrange
        CreateSut(_ => new HttpResponseMessage(status));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.Unavailable);
        Server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReportUnavailableWhenTheNetworkFails()
    {
        // Arrange
        CreateSut((_, _) => throw new HttpRequestException("network down"));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.Unavailable);
        Server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReportUnavailableWhenTheConnectionIsInterrupted()
    {
        // Arrange
        CreateSut((_, _) => throw new IOException("connection reset"));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.Unavailable);
    }

    [Fact]
    public async Task ItShouldReportUnavailableWhenTheRequestTimesOutWithoutCallerCancellation()
    {
        // Arrange
        CreateSut((_, _) => throw new TaskCanceledException("timeout"));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.Unavailable);
        Server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldPropagateCallerCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        CreateSut((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var action = () => Sut.UploadAsync(ValidUrl, content, ValidContentType, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task ItShouldReportUploadedForAnySuccessStatus(HttpStatusCode status)
    {
        // Arrange
        CreateSut(_ => new HttpResponseMessage(status));
        using var content = new MemoryStream([1]);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, ValidContentType, CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.Uploaded);
    }

    [Fact]
    public async Task ItShouldPutTheExactBytesToTheExactUrlWithTheContentTypeAndNoAuthorization()
    {
        // Arrange
        byte[] bytes = [9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 255];
        CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var content = new MemoryStream(bytes);

        // Act
        var outcome = await Sut.UploadAsync(ValidUrl, content, "image/png", CancellationToken.None);

        // Assert
        outcome.Should().Be(PhotographUploadOutcomeEnum.Uploaded);
        var request = Server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.Uri!.AbsoluteUri.Should().Be(ValidUrl);
        request.Authorization.Should().BeNull();
        Bodies.Should().ContainSingle().Which.Bytes.Should().Equal(bytes);
        Bodies[0].ContentType.Should().Be("image/png");
        MockHttpClientFactory.Received(1).CreateClient(AuthenticationHttpClientConstants.Anonymous);
        MockHttpClientFactory.DidNotReceive().CreateClient(AuthenticationHttpClientConstants.AuthorizedApi);
    }
}
