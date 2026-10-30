using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile.Features.Onboarding.Services;

public sealed class OnboardingStatusService(
    IProfileClient profileClient,
    IAuthenticationService authentication,
    IAccountSessionCoordinator sessionCoordinator,
    IPreferenceStore preferences,
    IOfflinePreparationService offlinePreparation) : IOnboardingStatusService
{
    private const string CompletedCachePrefix = "mobile.onboardingCompleted.";
    private const string CompletedCacheValue = "1";

    public async Task<OnboardingStatusEnum> ResolveAsync(CancellationToken cancellationToken)
    {
        var session = authentication.CurrentSession;
        if (session is null)
        {
            return OnboardingStatusEnum.Unknown;
        }

        var lease = sessionCoordinator.Capture();
        if (lease is null || lease.UserId != session.UserId)
        {
            return OnboardingStatusEnum.Unknown;
        }

        var expectedCapability = new OfflineCapabilityModel(session.Subject, session.UserId, IsDeviceReady: true);
        if (HasCompletedCache(session.UserId))
        {
            var isPrepared = await offlinePreparation.IsPreparedAsync(expectedCapability, cancellationToken);
            if (!IsCurrent(lease, session))
            {
                return OnboardingStatusEnum.Unknown;
            }

            if (isPrepared)
            {
                return OnboardingStatusEnum.Completed;
            }
        }

        var result = await profileClient.GetAsync(cancellationToken);
        if (!IsCurrent(lease, session))
        {
            return OnboardingStatusEnum.Unknown;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            return OnboardingStatusEnum.Unknown;
        }

        if (!result.Value.OnboardingCompleted)
        {
            return OnboardingStatusEnum.Incomplete;
        }

        if (!await offlinePreparation.EnsurePreparedAsync(result.Value, cancellationToken))
        {
            return OnboardingStatusEnum.Unknown;
        }

        if (!IsCurrent(lease, session))
        {
            return OnboardingStatusEnum.Unknown;
        }

        RememberCompleted(lease.UserId);
        return OnboardingStatusEnum.Completed;
    }

    public void RememberCompleted(Guid userId)
    {
        preferences.Set(CompletedCachePrefix + userId.ToString("D"), CompletedCacheValue);
    }

    private bool HasCompletedCache(Guid userId)
    {
        return preferences.Get(CompletedCachePrefix + userId.ToString("D"), string.Empty) == CompletedCacheValue;
    }

    private bool IsCurrent(AccountSessionLeaseModel lease, AuthenticationSessionModel expectedSession)
    {
        var currentSession = authentication.CurrentSession;
        return sessionCoordinator.IsCurrent(lease) &&
            currentSession is not null &&
            currentSession.UserId == expectedSession.UserId &&
            string.Equals(currentSession.Subject, expectedSession.Subject, StringComparison.Ordinal);
    }
}
