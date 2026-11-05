using FishingLogBook.Mobile.Features.Dashboard.ViewModels;

namespace FishingLogBook.Mobile.Features.Dashboard.Services;

public interface IDashboardModalViewModelFactory
{
    DashboardFilterViewModel CreateFilters();
}
