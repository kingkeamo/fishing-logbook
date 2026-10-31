using FishingLogBook.Mobile.Features.Catch.Offline.Models;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public interface ILocalCatchCleanupStore
{
    Task<IReadOnlyList<LocalCatchCleanupCandidateModel>> ListCandidatesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> TryDeleteAsync(
        LocalCatchCleanupCandidateModel candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid Id, string RelativePath)>> ListPendingFileDeletionsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task QueueFileDeletionAsync(Guid userId, string relativePath, DateTimeOffset now, CancellationToken cancellationToken);

    Task TryDeleteFileAsync(Guid userId, Guid id, string relativePath, Func<bool> delete, CancellationToken cancellationToken);
}
