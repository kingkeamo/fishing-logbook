using System.Net;
using AwesomeAssertions;
using FishingLogBook.Infrastructure.HttpClients.Geoapify;
using FishingLogBook.Infrastructure.Tests.HttpClients.Geoapify.GeoapifyLocationLookupClientTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Tests.HttpClients.Geoapify.GeoapifyHttpClientResilienceTests;

public class WhenTestingResilience : BaseGeoapifyLocationLookupClientTest
{
    [Fact]
    public async Task ItShouldNotRetryTooManyRequests()
    {
        // Arrange
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{}", HttpStatusCode.TooManyRequests)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("geoapify-test");

        // Act
        using var response = await client.GetAsync("v1/geocode/reverse", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        handler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRetryATransientFailureOnce()
    {
        // Arrange
        var handler = new RecordingHandler((attempt, request, _) =>
            Task.FromResult(JsonResponse(
                request,
                "{}",
                attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("geoapify-test");

        // Act
        using var response = await client.GetAsync("v1/geocode/reverse", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldRetryAfterTheThreeSecondProviderTimeout()
    {
        // Arrange
        var handler = new RecordingHandler(async (attempt, request, cancellationToken) =>
        {
            if (attempt == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return JsonResponse(request, "{}");
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("geoapify-test");

        // Act
        using var response = await client.GetAsync("v1/geocode/reverse", CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldNotWriteCoordinatesOrProviderKeysToHttpLogs()
    {
        // Arrange
        var logs = new RecordingLoggerProvider();
        var handler = new RecordingHandler((_, request, _) =>
            Task.FromResult(JsonResponse(request, "{}", HttpStatusCode.TooManyRequests)));
        using var provider = CreateProvider(handler, logs);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("geoapify-test");

        // Act
        using var response = await client.GetAsync(
            "v1/geocode/reverse?lat=53.3498&lon=-6.2603&apiKey=private-key",
            CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        logs.Messages.Should().NotContain(message =>
            message.Contains("53.3498", StringComparison.Ordinal)
            || message.Contains("-6.2603", StringComparison.Ordinal)
            || message.Contains("private-key", StringComparison.Ordinal));
    }

    private static ServiceProvider CreateProvider(
        RecordingHandler handler,
        ILoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });
        services.AddHttpClient("geoapify-test", client =>
            {
                client.BaseAddress = new Uri("https://geoapify.test/");
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddGeoapifyResilience();
        return services.BuildServiceProvider();
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(Messages);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<string> _messages;

        public RecordingLogger(List<string> messages)
        {
            _messages = messages;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
