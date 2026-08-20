namespace FishingLogBook.Web.Features.Onboarding.Services;

public interface IOnboardingService
{
    Task<bool> IsCompletedAsync(CancellationToken cancellationToken);

    Task CompleteAsync(CancellationToken cancellationToken);
}
