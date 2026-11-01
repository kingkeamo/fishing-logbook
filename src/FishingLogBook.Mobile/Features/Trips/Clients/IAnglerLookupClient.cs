using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Clients;

public interface IAnglerLookupClient
{
    Task<ApiResultModel<List<AnglerSummaryDto>>> FindAsync(string query, CancellationToken cancellationToken);
}
