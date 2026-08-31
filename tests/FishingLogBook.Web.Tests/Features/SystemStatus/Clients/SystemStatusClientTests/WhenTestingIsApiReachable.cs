using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Clients.SystemStatusClientTests;

public class WhenTestingIsApiReachable : BaseSystemStatusClientTest
{
    [Fact]
    public async Task ItShouldTreatAnyHttpResponseAsReachableWithoutUsingCache()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, "{}");

        // Act
        var reachable = await CreateClient(handler).IsApiReachableAsync(CancellationToken.None);

        // Assert
        reachable.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/health");
        handler.LastRequest.Headers.CacheControl.Should().BeEquivalentTo(new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        });
        handler.LastRequest.Headers.Pragma.ToString().Should().Be("no-cache");
    }

    [Fact]
    public async Task ItShouldTreatAClientErrorResponseAsReachable()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NotFound, "{}");

        // Act
        var reachable = await CreateClient(handler).IsApiReachableAsync(CancellationToken.None);

        // Assert
        reachable.Should().BeTrue();
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/health");
    }

    [Fact]
    public async Task ItShouldTreatAnHttpFailureWithAStatusCodeAsReachable()
    {
        // Arrange
        var handler = new ThrowingHandler(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));

        // Act
        var reachable = await CreateClient(handler).IsApiReachableAsync(CancellationToken.None);

        // Assert
        reachable.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldTreatANetworkFailureWithoutAStatusCodeAsUnreachable()
    {
        // Arrange
        var handler = new ThrowingHandler(new HttpRequestException("connection refused"));

        // Act
        var reachable = await CreateClient(handler).IsApiReachableAsync(CancellationToken.None);

        // Assert
        reachable.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatATimeoutAsUnreachable()
    {
        // Arrange
        var handler = new ThrowingHandler(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

        // Act
        var reachable = await CreateClient(handler).IsApiReachableAsync(CancellationToken.None);

        // Assert
        reachable.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRethrowWhenTheCallerCancels()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var handler = new HangingHandler();

        // Act
        var act = () => CreateClient(handler).IsApiReachableAsync(cancelled.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(exception);
        }
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The health probe should have been cancelled.");
        }
    }
}
