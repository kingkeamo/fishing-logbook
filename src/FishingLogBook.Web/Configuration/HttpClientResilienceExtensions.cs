using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace FishingLogBook.Web.Configuration;

internal static class HttpClientResilienceExtensions
{
    internal const string PipelineName = "get-read-retry";
    internal const int MaxRetryAttempts = 2;
    internal static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    internal static IHttpClientBuilder ConfigureGetReadResilience(this IHttpClientBuilder builder)
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
