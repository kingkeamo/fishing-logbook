using FishingLogBook.Mobile.Features.Authentication.Pages;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Settings.Services;

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
        Page page = _authenticationService.CurrentSession is null
            ? _services.GetRequiredService<AuthenticationPage>()
            : _services.GetRequiredService<AppShell>();
        _window = new Window(page);
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
                ? _services.GetRequiredService<AuthenticationPage>()
                : _services.GetRequiredService<AppShell>();
        });
    }
}
