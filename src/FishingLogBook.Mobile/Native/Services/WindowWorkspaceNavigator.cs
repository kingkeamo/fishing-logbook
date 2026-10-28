using FishingLogBook.Mobile.Features.Onboarding.Pages;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class WindowWorkspaceNavigator(IServiceProvider services) : IOnboardingNavigator
{
    public void ShowOnboarding()
    {
        Present(provider => provider.GetRequiredService<OnboardingPage>());
    }

    public void ShowAuthenticatedApp()
    {
        Present(provider => provider.GetRequiredService<AppShell>());
    }

    private void Present(Func<IServiceProvider, Page> factory)
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window is null)
        {
            return;
        }

        window.Dispatcher.Dispatch(() => window.Page = factory(services));
    }
}
