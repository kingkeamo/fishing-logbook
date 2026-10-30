using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public sealed class PhotographFileStore(
    IFileSystemAdapter fileSystem,
    IPhotographRootPathProvider rootPathProvider,
    ILogger<PhotographFileStore> logger) : IPhotographFileStore
{
    public string GetAbsolutePath(Guid userId, string relativeFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeFilePath);
        if (!TryBuildOwnedAbsolutePath(userId, relativeFilePath, out var absolutePath))
        {
            throw new InvalidOperationException("Photograph path is outside the account photograph folder.");
        }

        return absolutePath;
    }

    public string GetAccountDirectory(Guid userId)
    {
        var directory = Path.Combine(rootPathProvider.GetPhotographRootDirectory(), userId.ToString("N"));
        fileSystem.CreateDirectory(directory);
        return directory;
    }

    public async Task<string?> SaveAsync(
        Guid userId,
        Guid photographId,
        string contentType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        cancellationToken.ThrowIfCancellationRequested();
        var extension = ExtensionFor(contentType);
        if (extension is null)
        {
            return null;
        }

        var relativePath = $"{userId:N}/{photographId:N}{extension}";
        var durablePath = GetAbsolutePath(userId, relativePath);
        var directory = Path.GetDirectoryName(durablePath);
        if (directory is not null)
        {
            fileSystem.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(directory ?? GetAccountDirectory(userId), $".{photographId:N}.tmp");
        try
        {
            await WriteBytesAsync(tempPath, bytes, cancellationToken);
            fileSystem.MoveFile(tempPath, durablePath, overwrite: true);
            return NormalizeRelativePath(relativePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Durable photograph write failed for user {UserId}.", userId);
            TryDelete(tempPath);
            return null;
        }
    }

    public bool TryResolveRelativePath(Guid userId, string relativeFilePath, out string absolutePath)
    {
        if (!TryBuildOwnedAbsolutePath(userId, relativeFilePath, out absolutePath))
        {
            absolutePath = string.Empty;
            return false;
        }

        return fileSystem.FileExists(absolutePath);
    }

    private bool TryBuildOwnedAbsolutePath(Guid userId, string relativeFilePath, out string absolutePath)
    {
        absolutePath = string.Empty;
        var normalized = NormalizeRelativePath(relativeFilePath);
        if (normalized.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized)
            || !normalized.StartsWith($"{userId:N}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var root = Path.GetFullPath(rootPathProvider.GetPhotographRootDirectory());
        var candidate = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var accountRoot = Path.GetFullPath(Path.Combine(root, userId.ToString("N")))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(accountRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                candidate.TrimEnd(Path.DirectorySeparatorChar),
                accountRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    private static string? ExtensionFor(string contentType)
    {
        if (string.Equals(contentType, PhotographContentTypeConstants.Jpeg, StringComparison.OrdinalIgnoreCase))
        {
            return ".jpg";
        }

        if (string.Equals(contentType, PhotographContentTypeConstants.Png, StringComparison.OrdinalIgnoreCase))
        {
            return ".png";
        }

        if (string.Equals(contentType, PhotographContentTypeConstants.Webp, StringComparison.OrdinalIgnoreCase))
        {
            return ".webp";
        }

        return null;
    }

    private static string NormalizeRelativePath(string relativeFilePath)
    {
        return relativeFilePath.Replace('\\', '/');
    }

    private async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.Create(path);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
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
            logger.LogError(exception, "Temporary photograph file cleanup failed.");
        }
    }
}
