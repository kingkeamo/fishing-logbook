using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Services;
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
        services.AddScoped<INetworkStatus, BrowserNetworkStatus>();
        services.AddScoped<ITestCatchJsonStore, IndexedDbTestCatchJsonStore>();
        services.AddScoped<ITestCatchStore, TestCatchStore>();
        services.AddScoped<ITestCatchPhotoStore, IndexedDbTestCatchPhotoStore>();
        services.AddScoped<ITestCatchSynchroniser, TestCatchSynchroniser>();
        services.AddSingleton<DiagnosticStatus>();
        services.AddScoped<IDiagnosticEventStore, IndexedDbDiagnosticEventStore>();
        services.AddScoped<IDiagnosticLogger, DiagnosticLogger>();
        services.AddScoped<IDiagnosticSynchroniser, DiagnosticSynchroniser>();
        services.AddLocalization();
        services.AddScoped<ICultureService, CultureService>();
        services.AddMudServices();
        services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();

        return services;
    }
}
