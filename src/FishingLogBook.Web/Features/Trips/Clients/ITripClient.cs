using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Trips.Clients;

public interface ITripClient
{
    Task<TripDto?> UpsertAsync(TripDto trip, CancellationToken cancellationToken);
}
