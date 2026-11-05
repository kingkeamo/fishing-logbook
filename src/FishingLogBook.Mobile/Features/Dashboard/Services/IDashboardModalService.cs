using FishingLogBook.Mobile.Features.Dashboard.Models;

namespace FishingLogBook.Mobile.Features.Dashboard.Services;

public interface IDashboardModalService
{
    Task<DashboardFilterSelectionModel?> EditFiltersAsync(
        DashboardFilterSelectionModel selection,
        DashboardFilterOptionsModel options,
        CancellationToken cancellationToken);
}
