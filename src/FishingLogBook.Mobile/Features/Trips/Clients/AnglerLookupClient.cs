using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Clients;

public sealed class AnglerLookupClient(AuthorizedApiClient apiClient) : IAnglerLookupClient
{
    public Task<ApiResultModel<List<AnglerSummaryDto>>> FindAsync(string query, CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<List<AnglerSummaryDto>>($"api/profiles/lookup?q={Uri.EscapeDataString(query)}", cancellationToken);
    }
}
