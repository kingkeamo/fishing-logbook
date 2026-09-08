using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace FishingLogBook.Infrastructure.HttpClients.Geoapify;

public static class GeoapifyHttpClientResilienceExtensions
{
    public const string PipelineName = "geoapify-location-lookup";
    public const int MaxRetryAttempts = 1;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    public static IHttpClientBuilder AddGeoapifyResilience(this IHttpClientBuilder builder)
    {
        builder.RemoveAllLoggers();
        builder.AddResilienceHandler(
            PipelineName,
            static pipeline =>
            {
                var retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = MaxRetryAttempts,
                    Delay = RetryDelay,
                    BackoffType = DelayBackoffType.Constant,
                    UseJitter = true,
                    ShouldRetryAfterHeader = false
                };
                retry.DisableForUnsafeHttpMethods();
                var defaultShouldHandle = retry.ShouldHandle;
                retry.ShouldHandle = args => args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests
                    ? ValueTask.FromResult(false)
                    : defaultShouldHandle(args);

                pipeline.AddRetry(retry);
                pipeline.AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = RequestTimeout
                });
            });
        return builder;
    }
}
