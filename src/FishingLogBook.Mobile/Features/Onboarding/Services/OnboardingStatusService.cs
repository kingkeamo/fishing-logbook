using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile.Features.Onboarding.Services;

public sealed class OnboardingStatusService(
    IProfileClient profileClient,
    IAuthenticationService authentication,
    IAccountSessionCoordinator sessionCoordinator,
    IPreferenceStore preferences) : IOnboardingStatusService
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

        if (HasCompletedCache(session.UserId))
        {
            return OnboardingStatusEnum.Completed;
        }

        var lease = sessionCoordinator.Capture();
        if (lease is null || lease.UserId != session.UserId)
        {
            return OnboardingStatusEnum.Unknown;
        }

        var result = await profileClient.GetAsync(cancellationToken);
        if (!sessionCoordinator.IsCurrent(lease) || authentication.CurrentSession?.UserId != lease.UserId)
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
}
