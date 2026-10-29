using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Onboarding.Pages;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Features.Startup.Pages;

namespace FishingLogBook.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly PreferenceService _preferences;
    private readonly IAuthenticationService _authenticationService;
    private Window? _window;

    public App(
        PreferenceService preferences,
        IAuthenticationService authenticationService,
        IServiceProvider services)
    {
        InitializeComponent();
        _preferences = preferences;
        _authenticationService = authenticationService;
        _services = services;
        _authenticationService.SessionChanged += OnSessionChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _preferences.Initialize();
        _window = new Window(_services.GetRequiredService<StartupPage>());
        return _window;
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        var window = _window;
        if (window is null)
        {
            return;
        }

        window.Dispatcher.Dispatch(() =>
        {
            window.Page = _authenticationService.CurrentSession is null
                ? _services.GetRequiredService<StartupPage>()
                : _services.GetRequiredService<OnboardingGatePage>();
        });
    }
}
