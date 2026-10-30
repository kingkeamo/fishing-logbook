namespace FishingLogBook.Mobile.Features.Photographs.Offline.Stores;

public interface IPhotographReferenceStore
{
    Task<IReadOnlySet<string>> GetReferencedRelativePathsAsync(Guid userId, CancellationToken cancellationToken);
}
