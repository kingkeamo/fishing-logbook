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

    Task<bool> IsFileReferencedAsync(
        Guid userId,
        string relativePath,
        CancellationToken cancellationToken);

    Task CompleteFileDeletionAsync(Guid id, CancellationToken cancellationToken);
}
