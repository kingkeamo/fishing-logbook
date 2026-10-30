using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
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
    private readonly ISessionLifecycleCoordinator _sessionLifecycle;
    private Window? _window;

    public App(
        PreferenceService preferences,
        IAuthenticationService authenticationService,
        ISessionLifecycleCoordinator sessionLifecycle,
        IServiceProvider services)
    {
        InitializeComponent();
        _preferences = preferences;
        _authenticationService = authenticationService;
        _sessionLifecycle = sessionLifecycle;
        _services = services;
        _authenticationService.SessionChanged += OnSessionChanged;
        _sessionLifecycle.StateChanged += OnLifecycleStateChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _preferences.Initialize();
        _window = new Window(_services.GetRequiredService<StartupPage>());
        _sessionLifecycle.Start();
        _window.Resumed += OnResumed;
        _window.Stopped += OnStopped;
        _window.Destroying += OnDestroying;
        return _window;
    }

    private async void OnResumed(object? sender, EventArgs args)
    {
        try
        {
            await _sessionLifecycle.ForegroundAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnStopped(object? sender, EventArgs args)
    {
        _sessionLifecycle.Background();
    }

    private void OnDestroying(object? sender, EventArgs args)
    {
        _sessionLifecycle.Background();
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

    private void OnLifecycleStateChanged(object? sender, EventArgs args)
    {
        var window = _window;
        if (window is null || _sessionLifecycle.State != SessionLifecycleStateEnum.SignInRequired)
        {
            return;
        }

        window.Dispatcher.Dispatch(() => window.Page = _services.GetRequiredService<StartupPage>());
    }
}
