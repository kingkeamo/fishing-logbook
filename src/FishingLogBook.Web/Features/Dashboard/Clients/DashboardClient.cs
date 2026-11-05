using System.Net.Http.Json;
using FishingLogBook.Shared.Dashboard.Builders;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Dashboard.Clients;

public sealed class DashboardClient(IHttpClientFactory factory) : IDashboardClient
{
    public async Task<DashboardDto> GetAsync(DashboardFilterDto filter, CancellationToken cancellationToken)
    {
        var client = factory.CreateClient(HttpClientNames.AuthorizedApi);
        return await client.GetFromJsonAsync<DashboardDto>(DashboardRequestBuilder.Build(filter), cancellationToken)
            ?? throw new HttpRequestException("Dashboard response was missing.");
    }
}
