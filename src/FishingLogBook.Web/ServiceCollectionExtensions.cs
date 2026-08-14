using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Localization;
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
        services.AddLocalization();
        services.AddScoped<ICultureService, CultureService>();
        services.AddMudServices();
        services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();

        return services;
    }
}
