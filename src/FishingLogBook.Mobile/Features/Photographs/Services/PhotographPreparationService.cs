using FishingLogBook.Mobile.Features.Photographs.Constants;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Models;
using FishingLogBook.Shared.Photographs.Services;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public sealed class PhotographPreparationService(
    PhotographMetadataProcessor metadataProcessor,
    IPhotographFileStore fileStore,
    IFileSystemAdapter fileSystem,
    TimeProvider timeProvider,
    ILogger<PhotographPreparationService> logger) : IPhotographPreparationService
{
    public long MaxPhotographBytes { get; } = 10 * 1024 * 1024;

    public async Task<PhotographPreparationResultModel> PrepareFromFileAsync(
        Guid userId,
        string sourceFilePath,
        string contentType,
        DateTimeOffset? fileLastModified,
        PhotographSourceEnum source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PhotographHeicContentTypeConstants.IsHeic(contentType))
        {
            return PhotographPreparationResultModel.Unsupported;
        }

        if (!PhotographContentTypeConstants.IsAllowed(contentType))
        {
            return PhotographPreparationResultModel.Unsupported;
        }

        var original = await ReadBytesAsync(sourceFilePath, cancellationToken);
        if (original is null)
        {
            return PhotographPreparationResultModel.CouldNotPrepare;
        }

        if (original.LongLength > MaxPhotographBytes)
        {
            return PhotographPreparationResultModel.TooLarge;
        }

        var now = timeProvider.GetUtcNow();
        var metadata = source == PhotographSourceEnum.Camera
            ? PhotographMetadataModel.Empty
            : await ReadMetadataAsync(original, contentType, fileLastModified, now, cancellationToken);
        var sanitised = Sanitise(original, contentType);
        if (sanitised is null)
        {
            return PhotographPreparationResultModel.CouldNotPrepare;
        }

        var photographId = Guid.NewGuid();
        var relativePath = await fileStore.SaveAsync(userId, photographId, contentType, sanitised, cancellationToken);
        if (relativePath is null)
        {
            return PhotographPreparationResultModel.CouldNotPrepare;
        }

        var absolutePath = fileStore.GetAbsolutePath(userId, relativePath);
        return PhotographPreparationResultModel.Prepared(
            new PhotographPreparedFileModel
            {
                PhotographId = photographId,
                RelativeFilePath = relativePath,
                ContentType = contentType,
                AbsolutePath = absolutePath
            },
            metadata);
    }

    private async Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!fileSystem.FileExists(path))
            {
                return null;
            }

            await using var stream = fileSystem.OpenRead(path);
            if (stream.Length > MaxPhotographBytes)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Photograph source file could not be read.");
            return null;
        }
    }

    private async Task<PhotographMetadataModel> ReadMetadataAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            return await metadataProcessor.ReadAsync(bytes, contentType, fileLastModified, now, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Photograph metadata could not be read.");
            return PhotographMetadataModel.Empty;
        }
    }

    private byte[]? Sanitise(byte[] bytes, string contentType)
    {
        try
        {
            return metadataProcessor.Sanitise(bytes, contentType);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Photograph metadata could not be sanitised.");
            return null;
        }
    }
}
