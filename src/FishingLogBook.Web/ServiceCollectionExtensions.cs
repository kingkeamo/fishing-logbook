using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Services;
using FishingLogBook.Web.Localization;
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
        Uri apiBaseAddress)
    {
        services.AddSingleton(apiConfig);
        services.AddSingleton(diagnosticsConfig);
        services.AddScoped<CorrelationContext>();
        services.AddScoped(sp =>
        {
            var handler = new CorrelationDelegatingHandler(sp.GetRequiredService<CorrelationContext>())
            {
                InnerHandler = new HttpClientHandler()
            };
            return new HttpClient(handler) { BaseAddress = apiBaseAddress };
        });
        services.AddScoped<ISystemStatusClient, SystemStatusClient>();
        services.AddScoped<ITestCatchClient, TestCatchClient>();
        services.AddScoped<IDiagnosticClient, DiagnosticClient>();
        services.AddScoped<INetworkService, NetworkService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<ITestCatchJsonStore, IndexedDbTestCatchJsonStore>();
        services.AddScoped<ITestCatchStore, TestCatchStore>();
        services.AddScoped<ITestCatchPhotoStore, IndexedDbTestCatchPhotoStore>();
        services.AddScoped<ITestCatchSynchroniser, TestCatchSynchroniser>();
        services.AddSingleton<DiagnosticStatusModel>();
        services.AddScoped<ILoggingService, LoggingService>();
        services.AddScoped<IDiagnosticEventStore, IndexedDbDiagnosticEventStore>();
        services.AddScoped<IDiagnosticIndexedDbProbe, DiagnosticIndexedDbProbe>();
        services.AddScoped<IDiagnosticLogger, DiagnosticLogger>();
        services.AddScoped<IDiagnosticSynchroniser, DiagnosticSynchroniser>();
        services.AddLocalization();
        services.AddScoped<ICultureService, CultureService>();
        services.AddMudServices();
        services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();

        return services;
    }
}
