using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Dashboard.Clients;

public interface IDashboardClient
{
    Task<ApiResultModel<DashboardDto>> GetAsync(DashboardFilterDto filter, CancellationToken cancellationToken);
}
