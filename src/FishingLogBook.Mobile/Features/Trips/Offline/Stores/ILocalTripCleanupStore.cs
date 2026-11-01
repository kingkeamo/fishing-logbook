using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Stores;

public interface ILocalTripCleanupStore
{
    Task<IReadOnlyList<LocalTripCleanupCandidateModel>> ListCandidatesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> TryDeleteAsync(
        LocalTripCleanupCandidateModel candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid Id, string RelativePath)>> ListPendingFileDeletionsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task TryDeleteFileAsync(Guid userId, Guid id, string relativePath, Func<bool> delete, CancellationToken cancellationToken);
}
