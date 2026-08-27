using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface IActiveTripService
{
    event EventHandler? StateChanged;

    Task<TripModel?> GetActiveAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<TripModel> StartAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<TripModel> FinishAsync(TripModel trip, CancellationToken cancellationToken);

    Task<TripModel?> UpdateDetailsAsync(
        TripModel trip,
        string? title,
        string? placeName,
        CancellationToken cancellationToken);

    Task<TripModel?> TryAttachLocationAsync(TripModel trip, CancellationToken cancellationToken);

    void Invalidate();
}
