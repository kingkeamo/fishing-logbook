using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace FishingLogBook.Client.Resilience;

public static class HttpClientResilienceExtensions
{
    public const string PipelineName = "get-read-retry";
    public const int MaxRetryAttempts = 2;
    public static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    public static IHttpClientBuilder ConfigureGetReadResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler(
            PipelineName,
            static pipeline =>
            {
                var options = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = MaxRetryAttempts,
                    Delay = InitialRetryDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldRetryAfterHeader = true
                };
                options.DisableForUnsafeHttpMethods();

                var defaultShouldHandle = options.ShouldHandle;
                options.ShouldHandle = args =>
                {
                    if (args.Outcome.Result?.StatusCode == HttpStatusCode.NotFound)
                    {
                        return ValueTask.FromResult(false);
                    }

                    return defaultShouldHandle(args);
                };

                pipeline.AddRetry(options);
            });

        return builder;
    }
}
