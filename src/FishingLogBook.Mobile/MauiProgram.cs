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
using FishingLogBook.Mobile.Features.Photographs.Mappers;
using FishingLogBook.Mobile.Features.Photographs.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Photographs.ViewModels;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Shared.Catches.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Mobile.Features.Photographs.Clients;
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
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Native.Adapters;
using FishingLogBook.Mobile.Native.Browsers;
using FishingLogBook.Mobile.Native.Navigation;
using FishingLogBook.Mobile.Native.Services;
using FishingLogBook.Mobile.Native.Stores;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Shared.Photographs.Contracts;
using FishingLogBook.Shared.Photographs.Services;
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
        builder.Services.AddSingleton<IOfflineAccountStore>(services => new FileOfflineAccountStore(
            services.GetRequiredService<IFileSystemAdapter>(),
            Path.Combine(FileSystem.AppDataDirectory, "offline-accounts")));
        builder.Services.AddSingleton<ISqliteConnectionFactory>(services =>
        {
            var pathProvider = services.GetService<ILocalDatabasePathProvider>();
            var databasePath = pathProvider?.GetDatabasePath()
                ?? Path.Combine(FileSystem.AppDataDirectory, "local-logbook.db");
            return new SqliteConnectionFactory(databasePath);
        });
        builder.Services.AddSingleton<ISqliteMigrator, SqliteMigrator>();
        builder.Services.AddSingleton<PhotographOrphanCleanupService>();
        builder.Services.AddSingleton<IPhotographOrphanCleanupService>(services =>
            services.GetRequiredService<PhotographOrphanCleanupService>());
        builder.Services.AddSingleton<ILocalDatabaseInitializedObserver>(services =>
            services.GetRequiredService<PhotographOrphanCleanupService>());
        builder.Services.AddSingleton<ISqliteDatabase>(services => new SqliteDatabase(
            services.GetRequiredService<ISqliteConnectionFactory>(),
            services.GetRequiredService<ISqliteMigrator>(),
            services.GetRequiredService<ILogger<SqliteDatabase>>(),
            () => services.GetServices<ILocalDatabaseInitializedObserver>()));
        builder.Services.AddSingleton<IOutboxStore, SqliteOutboxStore>();
        builder.Services.AddSingleton<IOutboxChangeNotifier, OutboxChangeNotifier>();
        builder.Services.AddSingleton<OutboxBackoffPolicy>();
        builder.Services.AddSingleton<ICatchStore, SqliteCatchStore>();
        builder.Services.AddSingleton<ICatchSyncStateStore, SqliteCatchSyncStateStore>();
        builder.Services.AddSingleton<ICatchSyncStatusStore, SqliteCatchSyncStatusStore>();
        builder.Services.AddTransient<ICatchSyncClient, CatchSyncClient>();
        builder.Services.AddSingleton<IPhotographUploadClient, PhotographUploadClient>();
        builder.Services.AddSingleton<IOutboxOperationSynchroniser, CatchMetadataSynchroniser>();
        builder.Services.AddSingleton<IOutboxOperationSynchroniser, CatchPhotographSynchroniser>();
        builder.Services.AddSingleton<IOutboxSyncCoordinator, OutboxSyncCoordinator>();
        builder.Services.AddSingleton<ICatchSyncRetryService, CatchSyncRetryService>();
        builder.Services.AddSingleton<ICatchPhotographProposalService, CatchPhotographProposalService>();
        builder.Services.AddSingleton<CatchPresentationMapper>();
        builder.Services.AddSingleton<IAnglerPreferencesService, AnglerPreferencesService>();
        builder.Services.AddSingleton<IOfflinePreparationService, OfflinePreparationService>();
        builder.Services.AddSingleton<IAccountUnitsService, AccountUnitsService>();
        builder.Services.AddSingleton<IProfileEditorService, ProfileEditorService>();
        builder.Services.AddSingleton<IConnectivityService, DeviceConnectivityService>();
        builder.Services.AddSingleton<ISessionLifecycleCoordinator>(services => new SessionLifecycleCoordinator(
            services.GetRequiredService<IConnectivityService>(),
            services.GetRequiredService<IAuthenticationService>(),
            services.GetRequiredService<IOfflineSessionService>(),
            services.GetRequiredService<IAccountSessionCoordinator>(),
            services.GetRequiredService<TimeProvider>(),
            TimeSpan.FromSeconds(30),
            services.GetRequiredService<ILogger<SessionLifecycleCoordinator>>()));
        builder.Services.AddSingleton<IStartupNavigator, WindowStartupNavigator>();
        builder.Services.AddSingleton<IStartupCoordinator>(services => new StartupCoordinator(
            services.GetRequiredService<IConnectivityService>(),
            services.GetRequiredService<IAuthenticationService>(),
            services.GetRequiredService<IOfflineCapabilityService>(),
            services.GetRequiredService<IOfflinePreparationService>(),
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
        builder.Services.AddSingleton<IPhotographRootPathProvider>(services => new PhotographRootPathProvider(
            services.GetRequiredService<IFileSystemAdapter>(),
            () => FileSystem.AppDataDirectory,
            services.GetService<ILocalDatabasePathProvider>()));
        builder.Services.AddSingleton<IPhotographFileStore, PhotographFileStore>();
        builder.Services.AddSingleton<IPhotographReferenceStore, SqlitePhotographReferenceStore>();
        builder.Services.AddSingleton<IPhotographLocalTimeMapper, DevicePhotographLocalTimeMapper>();
        builder.Services.AddSingleton<PhotographMetadataProcessor>();
        builder.Services.AddSingleton<IPhotographPreparationService, PhotographPreparationService>();
        builder.Services.AddSingleton<IPhotographCollectionEditorService, PhotographCollectionEditorService>();
        builder.Services.AddSingleton<ICatchPhotographSaveArgsMapper, CatchPhotographSaveArgsMapper>();
        builder.Services.AddSingleton<IPhotographMediaAdapter, PhotographMediaAdapter>();
        builder.Services.AddTransient<PhotographCollectionEditorViewModel>();
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
        builder.Services.AddTransient<CatchEditViewModel>();
        builder.Services.AddTransient<CatchEditPage>();
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
