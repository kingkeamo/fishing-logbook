using FishingLogBook.Mobile.Features.Onboarding.Enums;

namespace FishingLogBook.Mobile.Features.Onboarding.Services;

public interface IOnboardingStatusService
{
    Task<OnboardingStatusEnum> ResolveAsync(CancellationToken cancellationToken);
    void RememberCompleted(Guid userId);
}
