using FishingLogBook.Mobile.Features.About.Pages;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Features.Dashboard.Pages;
using FishingLogBook.Mobile.Features.Import.Pages;
using FishingLogBook.Mobile.Features.Profile.Pages;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Features.Settings.Pages;
using FishingLogBook.Mobile.Features.Trips.Pages;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Native.Pages;
using FishingLogBook.Mobile.Navigation;
#if DEBUG
using FishingLogBook.Mobile.Features.Measurements.Pages;
using FishingLogBook.Mobile.Features.Showcase.Pages;
#endif

namespace FishingLogBook.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ISessionLifecycleCoordinator _sessionLifecycle;
    private readonly HeaderProfileViewModel _profile;
    private readonly NavigationService _navigation;
    private readonly UiTextService _text;
    private readonly MenuItem _retryConnection;

    public AppShell(
        DashboardPage dashboard,
        CatchSummaryPage summary,
        TripsPage trips,
        ImportPage import,
        SettingsPage settings,
        UiTextService text,
        NavigationService navigation,
        IAuthenticationService authenticationService,
        ISessionLifecycleCoordinator sessionLifecycle,
        HeaderProfileViewModel profile)
    {
        InitializeComponent();
        BindingContext = text;
        _authenticationService = authenticationService;
        _sessionLifecycle = sessionLifecycle;
        _profile = profile;
        _navigation = navigation;
        _text = text;
        _authenticationService.SessionChanged += OnSessionChanged;
        _sessionLifecycle.StateChanged += OnLifecycleStateChanged;
        _profile.PropertyChanged += (_, _) => UpdateProfile();
        UpdateProfile();
        _ = _profile.LoadAsync(CancellationToken.None);
        var primary = new FlyoutItem
        {
            Route = "main",
            FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems,
            FlyoutItemIsVisible = false
        };
        foreach (var route in RouteConstants.Primary)
        {
            var key = route.ToString();
            var tab = new Tab { Route = key.ToLowerInvariant(), Icon = "tab_" + key.ToLowerInvariant() + ".png" };
            tab.SetBinding(BaseShellItem.TitleProperty, new Binding("[" + key + "]", source: text));
            Page page = route switch
            {
                MobileRouteEnum.Dashboard => dashboard,
                MobileRouteEnum.Catches => summary,
                MobileRouteEnum.Trips => trips,
                MobileRouteEnum.Import => import,
                MobileRouteEnum.Settings => settings,
                _ => new PlannedPage(text, key)
            };
            tab.Items.Add(new ShellContent { Route = key.ToLowerInvariant() + "-root", Content = page });
            primary.Items.Add(tab);
        }

        Items.Add(primary);
        var profileItem = new MenuItem { IconImageSource = "tab_settings.png" };
        profileItem.SetBinding(MenuItem.TextProperty, new Binding("[Profile]", source: text));
        profileItem.Clicked += async (_, _) => await OpenProfileAsync();
        Items.Add(profileItem);
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
            await navigation.NavigateAsync(MobileRouteEnum.About);
        };
        Items.Add(about);
#if DEBUG
        var showcase = new MenuItem { IconImageSource = "icon_flask.png" };
        showcase.SetBinding(MenuItem.TextProperty, new Binding("[Showcase]", source: text));
        showcase.Clicked += async (_, _) =>
        {
            FlyoutIsPresented = false;
            await navigation.NavigateAsync(MobileRouteEnum.Showcase);
        };
        Items.Add(showcase);
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.Showcase), typeof(ShowcasePage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.MeasurementShowcase), typeof(MeasurementShowcasePage));
#endif
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.CatchEdit), typeof(CatchEditPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.TripDetail), typeof(TripDetailPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.TripEdit), typeof(TripEditPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.TripPlan), typeof(TripPlanPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.CatchDetail), typeof(CatchDetailPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.PhotoPreview), typeof(PhotoPreviewPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.Profile), typeof(ProfilePage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.About), typeof(AboutPage));
        UpdateLifecycleState();
    }

    private async void OnOpenProfile(object? sender, EventArgs args)
    {
        await OpenProfileAsync();
    }

    private async Task OpenProfileAsync()
    {
        FlyoutIsPresented = false;
        await _navigation.NavigateAsync(MobileRouteEnum.Profile);
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            _profile.RefreshSession();
            UpdateProfile();
            _ = _profile.LoadAsync(CancellationToken.None);
        });
    }

    private void UpdateProfile()
    {
        ProfileName.Text = _profile.DisplayName;
        ProfileEmail.Text = _profile.SecondaryEmail;
        ProfileEmail.IsVisible = _profile.HasSecondaryEmail;
        ProfileInitial.Text = _profile.Initial;
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
