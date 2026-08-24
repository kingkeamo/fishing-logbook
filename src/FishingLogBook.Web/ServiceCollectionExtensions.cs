using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Clients;
using FishingLogBook.Web.Features.Diagnostics.Http;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Features.OfflineAccess.Clients;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Offline;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Features.Users.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FishingLogBook.Web;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFishingLogBookWeb(
        this IServiceCollection services,
        ApiConfig apiConfig,
        DiagnosticsClientConfig diagnosticsConfig,
        AuthConfig authConfig,
        BuildMetadataConfig buildMetadata,
        Uri apiBaseAddress)
    {
        authConfig.EnsureRequired();
        services.AddSingleton(apiConfig);
        services.AddSingleton(diagnosticsConfig);
        services.AddSingleton(authConfig);
        services.AddSingleton(buildMetadata);
        services.AddScoped<CorrelationContext>();
        services.AddTransient<CorrelationDelegatingHandler>();
        services.AddTransient(sp => CreateApiAuthorizationMessageHandler(sp, authConfig, apiBaseAddress));
        RegisterHttpClients(services, apiBaseAddress);
        services.AddHttpClient<ISystemStatusClient, SystemStatusClient>(client =>
        {
            client.BaseAddress = apiBaseAddress;
        }).AddHttpMessageHandler<CorrelationDelegatingHandler>();
        services.AddHttpClient<IDiagnosticClient, DiagnosticClient>(client =>
        {
            client.BaseAddress = apiBaseAddress;
        }).AddHttpMessageHandler<CorrelationDelegatingHandler>();
        services.AddScoped<IProfileClient, ProfileClient>();
        services.AddScoped<IFishingPreferenceClient, FishingPreferenceClient>();
        services.AddScoped<IAnglerPreferencesStore, IndexedDbAnglerPreferencesStore>();
        services.AddScoped<IAnglerPreferencesProvider, AnglerPreferencesProvider>();
        services.AddScoped<IProfileSummaryProvider, ProfileSummaryProvider>();
        services.AddScoped<INetworkService, NetworkService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IInstallService, InstallService>();
        services.AddScoped<IAppUpdateService, AppUpdateService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IOfflineAccessPreferenceClient, OfflineAccessPreferenceClient>();
        services.AddScoped<IOfflineAccessDeviceService, OfflineAccessDeviceService>();
        services.AddScoped<IOfflineAccessService, OfflineAccessService>();
        services.AddScoped<IOfflineOwnerContextService, OfflineOwnerContextService>();
        services.AddScoped<IOfflineReconnectService, OfflineReconnectService>();
        services.AddScoped<ITimeService, TimeService>();
        services.AddScoped<ICatchStore, IndexedDbCatchStore>();
        services.AddScoped<ICatchSynchroniser, CatchSynchroniser>();
        services.AddScoped<ICatchClient, CatchClient>();
        services.AddScoped<ICurrentUserClient, CurrentUserClient>();
        services.AddScoped<ILocalCatchOwnerService, LocalCatchOwnerService>();
        services.AddScoped<IMeasurementService, MeasurementService>();
        services.AddScoped<ICatchDateGroupingService, CatchDateGroupingService>();
        services.AddSingleton<DiagnosticStatusModel>();
        services.AddScoped<ILoggingService, LoggingService>();
        services.AddScoped<IDiagnosticEventStore, IndexedDbDiagnosticEventStore>();
        services.AddScoped<IDiagnosticLogger, DiagnosticLogger>();
        services.AddScoped<IDiagnosticSynchroniser, DiagnosticSynchroniser>();
        services.AddLocalization();
        services.AddScoped<ICatalogueLocalizer, CatalogueLocalizer>();
        services.AddScoped<ICultureService, CultureService>();
        services.AddScoped<ISignedInUserDisplayService, SignedInUserDisplayService>();
        services.AddMudServices();
        services.AddScoped<IModalService, ModalService>();
        services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        services.AddOidcAuthentication(options => ConfigureOidc(options, authConfig));

        return services;
    }

    private static void RegisterHttpClients(IServiceCollection services, Uri apiBaseAddress)
    {
        services.AddHttpClient(HttpClientNames.AuthorizedApi, client =>
        {
            client.BaseAddress = apiBaseAddress;
        })
            .AddHttpMessageHandler<AuthorizationMessageHandler>()
            .AddHttpMessageHandler<CorrelationDelegatingHandler>();

        services.AddHttpClient(HttpClientNames.Anonymous, client =>
        {
            client.BaseAddress = apiBaseAddress;
        }).AddHttpMessageHandler<CorrelationDelegatingHandler>();
    }

    private static AuthorizationMessageHandler CreateApiAuthorizationMessageHandler(
        IServiceProvider services,
        AuthConfig authConfig,
        Uri apiBaseAddress)
    {
        var handler = new AuthorizationMessageHandler(
            services.GetRequiredService<IAccessTokenProvider>(),
            services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>());
        var authorizedUrl = apiBaseAddress.ToString().TrimEnd('/');
        handler.ConfigureHandler([authorizedUrl], [authConfig.ApiScope]);
        return handler;
    }

    private static void ConfigureOidc(
        Microsoft.AspNetCore.Components.WebAssembly.Authentication.RemoteAuthenticationOptions<OidcProviderOptions> options,
        AuthConfig authConfig)
    {
        options.ProviderOptions.Authority = authConfig.Authority;
        options.ProviderOptions.ClientId = authConfig.ClientId;
        options.ProviderOptions.ResponseType = "code";
        options.ProviderOptions.DefaultScopes.Clear();
        options.ProviderOptions.DefaultScopes.Add("openid");
        options.ProviderOptions.DefaultScopes.Add("profile");
        options.ProviderOptions.DefaultScopes.Add("email");
        options.ProviderOptions.DefaultScopes.Add(authConfig.ApiScope);
        options.ProviderOptions.AdditionalProviderParameters["resource"] = authConfig.ApiResource;
    }
}
