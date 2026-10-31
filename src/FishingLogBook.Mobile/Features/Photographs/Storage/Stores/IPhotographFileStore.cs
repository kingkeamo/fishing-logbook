namespace FishingLogBook.Mobile.Features.Photographs.Storage.Stores;

public interface IPhotographFileStore
{
    string GetAbsolutePath(Guid userId, string relativeFilePath);

    string GetAccountDirectory(Guid userId);

    Task<string?> SaveAsync(
        Guid userId,
        Guid photographId,
        string contentType,
        byte[] bytes,
        CancellationToken cancellationToken);

    bool TryResolveRelativePath(Guid userId, string relativeFilePath, out string absolutePath);

    bool TryDelete(Guid userId, string relativeFilePath);
}
