namespace FishingLogBook.Mobile.Features.Photographs.Services;

public interface IPhotographReferenceStore
{
    Task<IReadOnlySet<string>> GetReferencedRelativePathsAsync(Guid userId, CancellationToken cancellationToken);
}
