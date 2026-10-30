using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Photographs.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.IO.Adapters;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public sealed class PhotographOrphanCleanupService(
    IFileSystemAdapter fileSystem,
    IPhotographRootPathProvider rootPathProvider,
    IPhotographReferenceStore referenceStore,
    ISqliteDatabase database,
    ILogger<PhotographOrphanCleanupService> logger) : IPhotographOrphanCleanupService, ILocalDatabaseInitializedObserver
{
    public Task OnDatabaseInitializedAsync(CancellationToken cancellationToken)
    {
        return CleanupAllAccountsCoreAsync(cancellationToken);
    }

    public async Task CleanupAllAccountsAsync(CancellationToken cancellationToken)
    {
        await database.EnsureInitializedAsync(cancellationToken);
        await CleanupAllAccountsCoreAsync(cancellationToken);
    }

    private async Task CleanupAllAccountsCoreAsync(CancellationToken cancellationToken)
    {
        var root = rootPathProvider.GetPhotographRootDirectory();
        if (!fileSystem.DirectoryExists(root))
        {
            return;
        }

        foreach (var accountDirectory in Directory.EnumerateDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderName = Path.GetFileName(accountDirectory);
            if (!Guid.TryParseExact(folderName, "N", out var userId))
            {
                continue;
            }

            await CleanupAccountAsync(userId, accountDirectory, cancellationToken);
        }
    }

    private async Task CleanupAccountAsync(Guid userId, string accountDirectory, CancellationToken cancellationToken)
    {
        var referenced = await referenceStore.GetReferencedRelativePathsAsync(userId, cancellationToken);
        foreach (var file in fileSystem.EnumerateFiles(accountDirectory, "*.*"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            if (IsAbandonedTempFile(fileName))
            {
                TryDelete(file);
                continue;
            }

            var relative = $"{userId:N}/{fileName}".Replace('\\', '/');
            if (referenced.Contains(relative))
            {
                continue;
            }

            logger.LogDebug(
                "Retaining unreferenced durable photograph {RelativePath} for account {UserId}; it may be a prepared draft not yet saved to SQLite.",
                relative,
                userId);
        }
    }

    private static bool IsAbandonedTempFile(string fileName)
    {
        return fileName.StartsWith(".", StringComparison.Ordinal)
            && fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private void TryDelete(string path)
    {
        try
        {
            if (fileSystem.FileExists(path))
            {
                fileSystem.DeleteFile(path);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Orphan photograph cleanup failed for {Path}.", path);
        }
    }
}
