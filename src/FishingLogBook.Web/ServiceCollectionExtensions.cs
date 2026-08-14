using FishingLogBook.Web.Configuration;
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
        Uri apiBaseAddress)
    {
        services.AddSingleton(apiConfig);
        services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseAddress });
        services.AddScoped<ISystemStatusClient, SystemStatusClient>();
        services.AddScoped<ITestCatchClient, TestCatchClient>();
        services.AddScoped<INetworkStatus, BrowserNetworkStatus>();
        services.AddScoped<ITestCatchJsonStore, IndexedDbTestCatchJsonStore>();
        services.AddScoped<ITestCatchStore, TestCatchStore>();
        services.AddScoped<ITestCatchPhotoStore, IndexedDbTestCatchPhotoStore>();
        services.AddScoped<ITestCatchSynchroniser, TestCatchSynchroniser>();
        services.AddLocalization();
        services.AddScoped<ICultureService, CultureService>();
        services.AddMudServices();
        services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();

        return services;
    }
}
