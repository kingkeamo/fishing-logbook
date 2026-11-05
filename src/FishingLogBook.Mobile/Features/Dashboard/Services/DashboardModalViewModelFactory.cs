using FishingLogBook.Mobile.Features.Dashboard.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile.Features.Dashboard.Services;

public sealed class DashboardModalViewModelFactory(IServiceProvider services) : IDashboardModalViewModelFactory
{
    public DashboardFilterViewModel CreateFilters()
    {
        return services.GetRequiredService<DashboardFilterViewModel>();
    }
}
