namespace FishingLogBook.Web.Common.Offline.Dependencies;

public interface ITripDependencyService
{
    Task<bool> IsTripReadyForServerAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetTripsAwaitingCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken);
}
