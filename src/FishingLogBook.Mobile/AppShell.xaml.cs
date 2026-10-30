using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Features.Measurements.Pages;
using FishingLogBook.Mobile.Features.Profile.Pages;
using FishingLogBook.Mobile.Features.Settings.Pages;
using FishingLogBook.Mobile.Features.Showcase.Pages;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Native.Pages;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ISessionLifecycleCoordinator _sessionLifecycle;
    private readonly UiTextService _text;
    private readonly MenuItem _retryConnection;

    public AppShell(
        CatchSummaryPage summary,
        SettingsPage settings,
        UiTextService text,
        InteractionService interactions,
        NavigationService navigation,
        IAuthenticationService authenticationService,
        ISessionLifecycleCoordinator sessionLifecycle)
    {
        InitializeComponent();
        BindingContext = text;
        _authenticationService = authenticationService;
        _sessionLifecycle = sessionLifecycle;
        _text = text;
        _authenticationService.SessionChanged += OnSessionChanged;
        _sessionLifecycle.StateChanged += OnLifecycleStateChanged;
        UpdateProfile();
        var primary = new FlyoutItem { Route = "main", FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems };
        foreach (var route in RouteConstants.Primary)
        {
            var key = route.ToString();
            var tab = new Tab { Route = key.ToLowerInvariant(), Icon = "tab_" + key.ToLowerInvariant() + ".png" };
            tab.SetBinding(BaseShellItem.TitleProperty, new Binding("[" + key + "]", source: text));
            Page page = route switch
            {
                MobileRouteEnum.Catches => summary,
                MobileRouteEnum.Settings => settings,
                _ => new PlannedPage(text, key)
            };
            tab.Items.Add(new ShellContent { Route = key.ToLowerInvariant() + "-root", Content = page });
            primary.Items.Add(tab);
        }
        Items.Add(primary);
        var profile = new MenuItem { IconImageSource = "tab_settings.png" };
        profile.SetBinding(MenuItem.TextProperty, new Binding("[Profile]", source: text));
        profile.Clicked += async (_, _) =>
        {
            FlyoutIsPresented = false;
            await navigation.NavigateAsync(MobileRouteEnum.Profile);
        };
        Items.Add(profile);
        _retryConnection = new MenuItem();
        _retryConnection.SetBinding(MenuItem.TextProperty, new Binding("[RetryConnection]", source: text));
        _retryConnection.Clicked += async (_, _) =>
        {
            FlyoutIsPresented = false;
            await _sessionLifecycle.RetryAsync(CancellationToken.None);
        };
        var about = new MenuItem { IconImageSource = "icon_info.png" };
        about.SetBinding(MenuItem.TextProperty, new Binding("[About]", source: text));
        about.Clicked += async (_, _) =>
        {
            FlyoutIsPresented = false;
            await interactions.ShowModalAsync(text["About"], text["AboutBody"]);
        };
        Items.Add(about);
#if DEBUG
        var showcase = new MenuItem { IconImageSource = "icon_flask.png" };
        showcase.SetBinding(MenuItem.TextProperty, new Binding("[Showcase]", source: text));
        showcase.Clicked += async (_, _) => await navigation.NavigateAsync(MobileRouteEnum.Showcase);
        Items.Add(showcase);
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.Showcase), typeof(ShowcasePage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.MeasurementShowcase), typeof(MeasurementShowcasePage));
#endif
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.CatchDetail), typeof(CatchDetailPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.PhotoPreview), typeof(PhotoPreviewPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.Profile), typeof(ProfilePage));
        UpdateLifecycleState();
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(UpdateProfile);
    }

    private void UpdateProfile()
    {
        var email = _authenticationService.CurrentSession?.Email ?? string.Empty;
        ProfileName.Text = email;
        ProfileInitial.Text = email.Length == 0 ? "?" : email[..1].ToUpperInvariant();
    }

    private void OnLifecycleStateChanged(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(UpdateLifecycleState);
    }

    private void UpdateLifecycleState()
    {
        var state = _sessionLifecycle.State;
        if (state == SessionLifecycleStateEnum.RecoverableError && !Items.Contains(_retryConnection))
        {
            Items.Add(_retryConnection);
        }
        else if (state != SessionLifecycleStateEnum.RecoverableError && Items.Contains(_retryConnection))
        {
            Items.Remove(_retryConnection);
        }
        ConnectionStatus.IsVisible = state is SessionLifecycleStateEnum.LocalOffline or
            SessionLifecycleStateEnum.Recovering or
            SessionLifecycleStateEnum.RecoverableError;
        ConnectionStatus.Text = state switch
        {
            SessionLifecycleStateEnum.LocalOffline => _text["WorkingOffline"],
            SessionLifecycleStateEnum.Recovering => _text["RestoringConnection"],
            SessionLifecycleStateEnum.RecoverableError => _text["ConnectionRecoveryFailed"],
            _ => string.Empty
        };
    }
}
