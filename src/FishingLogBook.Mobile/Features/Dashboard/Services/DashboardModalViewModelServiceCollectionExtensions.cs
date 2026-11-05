using FishingLogBook.Mobile.Features.Dashboard.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile.Features.Dashboard.Services;

public static class DashboardModalViewModelServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardModalViewModels(this IServiceCollection services)
    {
        services.AddTransient<DashboardFilterViewModel>();
        services.AddSingleton<IDashboardModalViewModelFactory, DashboardModalViewModelFactory>();
        return services;
    }
}
