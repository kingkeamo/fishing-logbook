using FishingLogBook.Mobile.Features.Authentication.Pages;
using FishingLogBook.Mobile.Features.Onboarding.Pages;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Native.Navigation;

public sealed class WindowStartupNavigator(IServiceProvider services) : IStartupNavigator
{
    public void ShowSignIn()
    {
        Present(provider => provider.GetRequiredService<AuthenticationPage>());
    }

    public void ShowOnlineSession()
    {
        Present(provider => provider.GetRequiredService<OnboardingGatePage>());
    }

    public void ShowOfflineSession()
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
