using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Dashboard.Clients;

public interface IDashboardClient
{
    Task<DashboardDto> GetAsync(DashboardFilterDto filter, CancellationToken cancellationToken);
}
