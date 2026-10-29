using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using Polly;
using Polly.Retry;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public sealed class OidcRefreshService(
    IOidcProtocolClient oidcClient,
    TimeProvider timeProvider) : IOidcRefreshService
{
    public const int MaximumRetryAttempts = 1;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan LatestRetryDecision = TimeSpan.FromMilliseconds(3500);

    public async Task<OidcTokenResultModel> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        var pipeline = new ResiliencePipelineBuilder<OidcTokenResultModel>()
            .AddRetry(new RetryStrategyOptions<OidcTokenResultModel>
            {
                MaxRetryAttempts = MaximumRetryAttempts,
                Delay = RetryDelay,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.Failure == OidcFailureKindEnum.Transient &&
                    timeProvider.GetElapsedTime(startedAt) <= LatestRetryDecision)
            })
            .Build();

        return await pipeline.ExecuteAsync(
            async token => await oidcClient.RefreshAsync(refreshToken, token),
            cancellationToken);
    }
}
