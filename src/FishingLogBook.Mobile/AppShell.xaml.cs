using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Features.Settings.Pages;
using FishingLogBook.Mobile.Features.Showcase.Pages;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Native.Pages;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile;

public partial class AppShell : Shell
{
    public AppShell(CatchSummaryPage summary, SettingsPage settings, UiTextService text, InteractionService interactions, NavigationService navigation)
    {
        InitializeComponent();
        BindingContext = text;
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
#endif
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.CatchDetail), typeof(CatchDetailPage));
        Routing.RegisterRoute(RouteConstants.GetPath(MobileRouteEnum.PhotoPreview), typeof(PhotoPreviewPage));
    }
}
