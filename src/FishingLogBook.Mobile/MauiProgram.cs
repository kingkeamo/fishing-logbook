using CommunityToolkit.Maui;
using FishingLogBook.Client.Resilience;
using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Browsers;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Factories;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Features.Authentication.Pages;
using FishingLogBook.Mobile.Features.Authentication.Providers;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Authentication.Stores;
using FishingLogBook.Mobile.Features.Authentication.ViewModels;
using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Measurements.Pages;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using FishingLogBook.Mobile.Features.Onboarding.Pages;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Onboarding.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Pages;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Features.Settings.Pages;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Features.Showcase.Pages;
using FishingLogBook.Mobile.Features.Startup.Coordinators;
using FishingLogBook.Mobile.Features.Startup.Pages;
using FishingLogBook.Mobile.Features.Startup.ViewModels;
using FishingLogBook.Mobile.Features.Users.Clients;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Native.Adapters;
using FishingLogBook.Mobile.Native.Browsers;
using FishingLogBook.Mobile.Native.Navigation;
using FishingLogBook.Mobile.Native.Services;
using FishingLogBook.Mobile.Native.Stores;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        ConfigurePlatformHandlers();
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>().UseMauiCommunityToolkit();
        var authConfig = SelectPlatformCallbacks(MobileAuthConfig.Load());
        builder.Services.AddSingleton(authConfig);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<UiTextService>();
        ConfigurePlatformServices(builder.Services);
        builder.Services.AddSingleton<IPreferenceStore, DevicePreferenceStore>();
        builder.Services.AddSingleton<IAppearanceService, DeviceAppearanceService>();
        builder.Services.AddSingleton<PreferenceService>();
        builder.Services.AddSingleton<IRouteNavigator, ShellRouteNavigator>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<IInteractionPresenter, NativeInteractionPresenter>();
        builder.Services.AddSingleton<InteractionService>();
        builder.Services.AddSingleton<LocalDataService>();
        builder.Services.AddSingleton<SampleCatchProvider>();
        builder.Services.AddSingleton<IMeasurementService, MeasurementService>();
        builder.Services.AddSingleton<IMotionPreference, DeviceMotionPreference>();
        builder.Services.AddSingleton<IAuthenticationBrowser, MauiAuthenticationBrowser>();
        builder.Services.AddSingleton<ISecureAuthenticationStore, SecureAuthenticationStore>();
        builder.Services.AddSingleton<ISecureOfflineCapabilityStore, SecureOfflineCapabilityStore>();
        builder.Services.AddSingleton<IOfflineCapabilityService, OfflineCapabilityService>();
        builder.Services.AddSingleton<IOfflineSessionService, OfflineSessionService>();
        builder.Services.AddSingleton<IOidcProtocolClient>(services => new OidcProtocolClient(
            authConfig,
            services.GetRequiredService<TimeProvider>(),
            NoRedirectHttpMessageHandlerFactory.Create()));
        builder.Services.AddSingleton<IOidcRefreshService, OidcRefreshService>();
        builder.Services.AddSingleton<IAccountSessionCoordinator, AccountSessionCoordinator>();
        builder.Services.AddSingleton<AuthenticationService>();
        builder.Services.AddSingleton<IAuthenticationService>(
            services => services.GetRequiredService<AuthenticationService>());
        builder.Services.AddSingleton<IAccessTokenProvider>(
            services => services.GetRequiredService<AuthenticationService>());
        builder.Services.AddSingleton<AuthenticationCallbackHandler>();
        builder.Services.AddTransient<AuthenticationViewModel>();
        builder.Services.AddTransient<AuthenticationPage>();
        builder.Services.AddTransient<ApiBearerHandler>();
        builder.Services
            .AddHttpClient<ICurrentUserClient, CurrentUserClient>(client =>
                client.BaseAddress = new Uri(authConfig.ApiBaseUrl.TrimEnd('/') + "/"))
            .ConfigurePrimaryHttpMessageHandler(NoRedirectHttpMessageHandlerFactory.Create)
            .ConfigureGetReadResilience();
        builder.Services
            .AddHttpClient<ICognitoSessionClient, CognitoSessionClient>(client =>
                client.BaseAddress = new Uri(authConfig.HostedUiDomain.TrimEnd('/') + "/"))
            .ConfigurePrimaryHttpMessageHandler(NoRedirectHttpMessageHandlerFactory.Create);
        builder.Services
            .AddHttpClient<AuthorizedApiClient>(AuthenticationHttpClientConstants.AuthorizedApi, client =>
                client.BaseAddress = new Uri(authConfig.ApiBaseUrl.TrimEnd('/') + "/"))
            .ConfigurePrimaryHttpMessageHandler(NoRedirectHttpMessageHandlerFactory.Create)
            .AddHttpMessageHandler<ApiBearerHandler>()
            .ConfigureGetReadResilience();
        builder.Services.AddTransient<IFishingPreferenceClient, FishingPreferenceClient>();
        builder.Services.AddTransient<IFishingLocationClient, FishingLocationClient>();
        builder.Services.AddTransient<IProfileClient, ProfileClient>();
        builder.Services.AddSingleton<IFileSystemAdapter, FileSystemAdapter>();
        builder.Services.AddSingleton<IAnglerPreferencesStore>(services => new FileAnglerPreferencesStore(
            services.GetRequiredService<IFileSystemAdapter>(),
            Path.Combine(FileSystem.AppDataDirectory, "angler-preferences")));
        builder.Services.AddSingleton<IAnglerPreferencesService, AnglerPreferencesService>();
        builder.Services.AddSingleton<IAccountUnitsService, AccountUnitsService>();
        builder.Services.AddSingleton<IProfileEditorService, ProfileEditorService>();
        builder.Services.AddSingleton<IConnectivityService, DeviceConnectivityService>();
        builder.Services.AddSingleton<IStartupNavigator, WindowStartupNavigator>();
        builder.Services.AddSingleton<IStartupCoordinator>(services => new StartupCoordinator(
            services.GetRequiredService<IConnectivityService>(),
            services.GetRequiredService<IAuthenticationService>(),
            services.GetRequiredService<IOfflineCapabilityService>(),
            services.GetRequiredService<IDeviceUnlockService>(),
            services.GetRequiredService<IOfflineSessionService>(),
            services.GetRequiredService<IAccountSessionCoordinator>(),
            services.GetRequiredService<UiTextService>(),
            TimeSpan.FromSeconds(30)));
        builder.Services.AddTransient<StartupViewModel>();
        builder.Services.AddTransient<StartupOperationCoordinator>();
        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddSingleton<CatalogueTextService>();
        builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddSingleton<IOnboardingStatusService, OnboardingStatusService>();
        builder.Services.AddSingleton<IGeolocationAdapter, GeolocationAdapter>();
        builder.Services.AddSingleton<IDeviceLocationService, DeviceLocationService>();
        builder.Services.AddSingleton<IOnboardingNavigator, WindowWorkspaceNavigator>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<OnboardingGateViewModel>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<OnboardingGatePage>();
        builder.Services
            .AddHttpClient(AuthenticationHttpClientConstants.Anonymous)
            .ConfigurePrimaryHttpMessageHandler(NoRedirectHttpMessageHandlerFactory.Create);
        builder.Services.AddSingleton<CatchListViewModel>();
        builder.Services.AddSingleton<CatchSummaryPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<CatchDetailPage>();
        builder.Services.AddTransient<PhotoPreviewPage>();
        builder.Services.AddSingleton<ProfilePage>();
#if DEBUG
        ConfigurePlatformLogging(builder.Logging);
        builder.Services.AddTransient<ShowcasePage>();
        builder.Services.AddTransient<MeasurementShowcasePage>();
#endif
        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }

    static partial void ConfigurePlatformHandlers();

    static partial void ConfigurePlatformServices(IServiceCollection services);

    private static partial MobileAuthConfig SelectPlatformCallbacks(MobileAuthConfig config);

    static partial void ConfigurePlatformLogging(ILoggingBuilder logging);
}
