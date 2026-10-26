namespace FishingLogBook.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _services.GetRequiredService<Features.Settings.Services.PreferenceService>();
        return new Window(_services.GetRequiredService<AppShell>());
    }
}
