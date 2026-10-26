using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly PreferenceService _preferences;

    public App(PreferenceService preferences, IServiceProvider services)
    {
        InitializeComponent();
        _preferences = preferences;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _preferences.Initialize();
        return new Window(_services.GetRequiredService<AppShell>());
    }
}
