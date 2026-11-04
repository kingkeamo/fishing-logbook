using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dashboard.Builders;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Dashboard.Clients;

public sealed class DashboardClient(AuthorizedApiClient apiClient) : IDashboardClient
{
    public Task<ApiResultModel<DashboardDto>> GetAsync(DashboardFilterDto filter, CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<DashboardDto>(DashboardRequestBuilder.Build(filter), cancellationToken);
    }
}
