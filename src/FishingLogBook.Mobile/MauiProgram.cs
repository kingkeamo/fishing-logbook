using CommunityToolkit.Maui;
using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Settings.Pages;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Features.Showcase.Pages;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Native.Services;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        ConfigurePlatformHandlers();
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>().UseMauiCommunityToolkit();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<UiTextService>();
        builder.Services.AddSingleton<IPreferenceStore, DevicePreferenceStore>();
        builder.Services.AddSingleton<IAppearanceService, DeviceAppearanceService>();
        builder.Services.AddSingleton<PreferenceService>();
        builder.Services.AddSingleton<IRouteNavigator, ShellRouteNavigator>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<IInteractionPresenter, NativeInteractionPresenter>();
        builder.Services.AddSingleton<InteractionService>();
        builder.Services.AddSingleton<LocalDataService>();
        builder.Services.AddSingleton<SampleCatchProvider>();
        builder.Services.AddSingleton<CatchListViewModel>();
        builder.Services.AddSingleton<CatchSummaryPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<CatchDetailPage>();
        builder.Services.AddTransient<PhotoPreviewPage>();
#if DEBUG
        builder.Services.AddTransient<ShowcasePage>();
#endif
        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }

    static partial void ConfigurePlatformHandlers();
}
