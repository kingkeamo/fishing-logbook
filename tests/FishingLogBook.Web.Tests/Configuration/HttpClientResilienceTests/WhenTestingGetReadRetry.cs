using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Http;
using FishingLogBook.Web.Features.Diagnostics.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Configuration.HttpClientResilienceTests;

public class WhenTestingGetReadRetry : BaseHttpClientResilienceTest
{
    [Fact]
    public async Task ItShouldRetryGetRequestsAfterTransientNetworkFailures()
    {
        // Arrange
        var (client, handler) = CreateResilientClient((attempt, request, _) =>
        {
            if (attempt == 1)
            {
                throw new HttpRequestException("connection reset");
            }

            return Task.FromResult(CreateResponse(request, HttpStatusCode.OK));
        });

        // Act
        using var response = await client.GetAsync("api/catches", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.InvocationCount.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ItShouldRetryGetRequestsAfterTransientServerErrors(HttpStatusCode statusCode)
    {
        // Arrange
        var (client, handler) = CreateResilientClient((attempt, request, _) =>
            Task.FromResult(CreateResponse(
                request,
                attempt <= HttpClientResilienceExtensions.MaxRetryAttempts
                    ? statusCode
                    : HttpStatusCode.OK)));

        // Act
        using var response = await client.GetAsync("api/catches", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.InvocationCount.Should().Be(HttpClientResilienceExtensions.MaxRetryAttempts + 1);
    }

    [Fact]
    public async Task ItShouldRetryGetRequestsAfterRequestTimeout()
    {
        // Arrange
        var (client, handler) = CreateResilientClient((attempt, request, _) =>
            Task.FromResult(CreateResponse(
                request,
                attempt == 1 ? HttpStatusCode.RequestTimeout : HttpStatusCode.OK)));

        // Act
        using var response = await client.GetAsync("api/catches", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldRetryGetRequestsAfterTooManyRequests()
    {
        // Arrange
        var (client, handler) = CreateResilientClient((attempt, request, _) =>
            Task.FromResult(CreateResponse(
                request,
                attempt == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)));

        // Act
        using var response = await client.GetAsync("api/catches", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldNotRetryGetRequestsThatReturnNotFound()
    {
        // Arrange
        var (client, handler) = CreateResilientClient((attempt, request, _) =>
            Task.FromResult(CreateResponse(request, HttpStatusCode.NotFound)));

        // Act
        using var response = await client.GetAsync("api/catches/00000000-0000-0000-0000-000000000001", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        handler.InvocationCount.Should().Be(1);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task ItShouldNotRetryUnsafeHttpMethodsAfterTransientFailures(string methodName)
    {
        // Arrange
        var method = new HttpMethod(methodName);
        var (client, handler) = CreateResilientClient((attempt, request, _) =>
            Task.FromResult(CreateResponse(request, HttpStatusCode.ServiceUnavailable)));

        using var request = new HttpRequestMessage(method, "api/catches")
        {
            Content = method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch
                ? new StringContent("{}")
                : null
        };

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        handler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldStopRetryingWhenCancellationIsRequested()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var (client, handler) = CreateResilientClient(async (attempt, request, _) =>
        {
            if (attempt == 1)
            {
                await cancellation.CancelAsync();
                return CreateResponse(request, HttpStatusCode.InternalServerError);
            }

            return CreateResponse(request, HttpStatusCode.OK);
        });

        // Act
        Func<Task> act = async () =>
        {
            using var response = await client.GetAsync("api/catches", cancellation.Token);
            response.EnsureSuccessStatusCode();
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldKeepCorrelationHeadersAcrossGetRetries()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CorrelationContext>();
        services.AddTransient<CorrelationDelegatingHandler>();

        var primary = new CorrelationCapturingHandler((attempt, request, _) =>
            Task.FromResult(CreateResponse(
                request,
                attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)));

        services.AddHttpClient(HttpClientNames.Anonymous, client =>
            {
                client.BaseAddress = new Uri("https://api.test/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => primary)
            .AddHttpMessageHandler<CorrelationDelegatingHandler>()
            .ConfigureGetReadResilience();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(HttpClientNames.Anonymous);

        // Act
        using var response = await client.GetAsync("api/catches", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        primary.InvocationCount.Should().Be(2);
        primary.CorrelationIds.Should().HaveCount(2);
        primary.CorrelationIds.Should().OnlyContain(id => !string.IsNullOrWhiteSpace(id));
        primary.CorrelationIds.Distinct().Should().ContainSingle();
    }

    private sealed class CorrelationCapturingHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;
        private int _invocationCount;

        public CorrelationCapturingHandler(
            Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int InvocationCount => _invocationCount;

        public IReadOnlyList<string> CorrelationIds => _correlationIds;

        private readonly List<string> _correlationIds = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _invocationCount);
            if (request.Headers.TryGetValues(CorrelationHeaders.CorrelationId, out var values))
            {
                _correlationIds.Add(values.Single());
            }

            return await _responseFactory(attempt, request, cancellationToken);
        }
    }
}
