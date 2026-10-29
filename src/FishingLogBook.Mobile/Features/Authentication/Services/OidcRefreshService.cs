using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using Polly;
using Polly.Retry;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public sealed class OidcRefreshService : IOidcRefreshService
{
    public const int MaximumRetryAttempts = 1;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan RetrySafetyWindow = TimeSpan.FromSeconds(4);
    private readonly IOidcProtocolClient _oidcClient;
    private readonly TimeProvider _timeProvider;
    private readonly ResiliencePipeline<OidcTokenResultModel> _pipeline;

    public OidcRefreshService(IOidcProtocolClient oidcClient, TimeProvider timeProvider)
    {
        _oidcClient = oidcClient;
        _timeProvider = timeProvider;
        _pipeline = new ResiliencePipelineBuilder<OidcTokenResultModel>()
            .AddRetry(new RetryStrategyOptions<OidcTokenResultModel>
            {
                MaxRetryAttempts = MaximumRetryAttempts,
                Delay = RetryDelay,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                ShouldHandle = static args => ValueTask.FromResult(
                    args.Outcome.Result?.Failure == OidcFailureKindEnum.Transient)
            })
            .Build();
    }

    public async Task<OidcTokenResultModel> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetTimestamp();
        var dispatchedRequests = 0;
        OidcTokenResultModel? lastResult = null;
        return await _pipeline.ExecuteAsync(
            async token =>
            {
                if (dispatchedRequests > 0 &&
                    _timeProvider.GetElapsedTime(startedAt) > RetrySafetyWindow)
                {
                    return lastResult!;
                }

                dispatchedRequests++;
                lastResult = await _oidcClient.RefreshAsync(refreshToken, token);
                return lastResult;
            },
            cancellationToken);
    }
}
