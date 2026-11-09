using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Clients.PhotographDownloadClientTests;

public sealed class WhenTestingDownloadLimitsAndFailures
{
    private const string Url = "https://photos.example/image?signature=test";

    private readonly IHttpClientFactory _mockFactory = Substitute.For<IHttpClientFactory>();

    [Fact]
    public async Task ItShouldReportTooLargeWhenTheDeclaredLengthExceedsTheLimit()
    {
        // Arrange
        var sut = CreateSut(_ =>
        {
            var content = new ByteArrayContent([1]);
            content.Headers.ContentLength = PhotographDownloadClient.MaximumBytes + 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.TooLarge);
        result.Bytes.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReportTooLargeWhenTheStreamExceedsTheLimitWithoutADeclaredLength()
    {
        // Arrange
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new LimitlessStream(PhotographDownloadClient.MaximumBytes + 1))
        });

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.TooLarge);
        result.Bytes.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldAcceptAPhotographExactlyAtTheLimit()
    {
        // Arrange
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new LimitlessStream(PhotographDownloadClient.MaximumBytes))
        });

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.Downloaded);
        ((long)result.Bytes!.Length).Should().Be(PhotographDownloadClient.MaximumBytes);
    }

    [Fact]
    public async Task ItShouldReportUnavailableForAnEmptyBody()
    {
        // Arrange
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.Unavailable);
        result.Bytes.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ItShouldRejectAnExpiredOrMissingStorageUrl(HttpStatusCode status)
    {
        // Arrange
        var sut = CreateSut(_ => new HttpResponseMessage(status));

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.Rejected);
    }

    [Fact]
    public async Task ItShouldReportUnavailableWhenTheNetworkFails()
    {
        // Arrange
        var sut = CreateSut(_ => throw new HttpRequestException("offline"));

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.Unavailable);
    }

    [Fact]
    public async Task ItShouldReportUnavailableWhenTheRequestTimesOut()
    {
        // Arrange
        var sut = CreateSut(_ => throw new TaskCanceledException("timeout"));

        // Act
        var result = await sut.DownloadAsync(Url, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographDownloadOutcomeEnum.Unavailable);
    }

    [Fact]
    public async Task ItShouldPropagateACallerCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });

        // Act
        var act = () => sut.DownloadAsync(Url, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private PhotographDownloadClient CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var server = new RecordingHttpMessageHandler(respond);
        var http = new HttpClient(server);
        _mockFactory.CreateClient(AuthenticationHttpClientConstants.Anonymous).Returns(http);
        return new PhotographDownloadClient(_mockFactory, NullLogger<PhotographDownloadClient>.Instance);
    }

    private sealed class LimitlessStream(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = (int)Math.Min(count, length - _position);
            _position += remaining;
            return remaining;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
