namespace FishingLogBook.Mobile.Features.Photographs.Services;

public interface IPhotographOrphanCleanupService
{
    Task CleanupAllAccountsAsync(CancellationToken cancellationToken);
}
