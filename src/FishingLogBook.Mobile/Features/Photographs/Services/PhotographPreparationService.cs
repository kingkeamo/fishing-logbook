using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Providers;
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
        var original = await ReadBytesAsync(sourceFilePath, cancellationToken);
        if (original is null || original.Length == 0)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Photograph preparation failed at source read for {Source}.", source);
            return PhotographPreparationResultModel.CouldNotPrepare;
        }

        var detectedContentType = DetectContentType(original);
        logger.LogInformation(PhotographLoggerProvider.PipelineEvent,
            "Photograph source inspected: {Source}, {InspectedByteCount} bounded bytes, detected {Format}, declared type matches {TypeMatches}.",
            source, original.Length, detectedContentType ?? "unsupported", string.Equals(contentType, detectedContentType, StringComparison.OrdinalIgnoreCase));
        if (original.LongLength > MaxPhotographBytes)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Photograph preparation exceeded the {Limit} byte limit for {Source}.", MaxPhotographBytes, source);
            return PhotographPreparationResultModel.TooLarge;
        }

        if (detectedContentType is null)
        {
            return PhotographPreparationResultModel.Unsupported;
        }

        contentType = detectedContentType;
        var now = timeProvider.GetUtcNow();
        var metadata = source == PhotographSourceEnum.Camera
            ? PhotographMetadataModel.Empty
            : await ReadMetadataAsync(original, contentType, fileLastModified, now, cancellationToken);
        var sanitised = Sanitise(original, contentType);
        if (sanitised is null || sanitised.Length == 0)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Photograph preparation failed at sanitisation for {Source} with {Format}.", source, contentType);
            return PhotographPreparationResultModel.CouldNotPrepare;
        }

        if (sanitised.LongLength > MaxPhotographBytes)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Sanitised photograph exceeded the byte limit for {Source}.", source);
            return PhotographPreparationResultModel.TooLarge;
        }

        var photographId = Guid.NewGuid();
        var relativePath = await fileStore.SaveAsync(userId, photographId, contentType, sanitised, cancellationToken);
        if (relativePath is null)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Photograph preparation failed at durable save for {Source} with {Format}.", source, contentType);
            return PhotographPreparationResultModel.CouldNotPrepare;
        }

        logger.LogInformation(PhotographLoggerProvider.PipelineEvent, "Photograph durable save completed: {Source}, {Format}, {ByteCount} bytes.", source, contentType, sanitised.Length);
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

    private static string? DetectContentType(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return PhotographContentTypeConstants.Jpeg;
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return PhotographContentTypeConstants.Png;
        }

        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return PhotographContentTypeConstants.Webp;
        }

        return null;
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
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            while (buffer.Length <= MaxPhotographBytes)
            {
                var remaining = (int)Math.Min(chunk.Length, MaxPhotographBytes + 1 - buffer.Length);
                var read = await stream.ReadAsync(chunk.AsMemory(0, remaining), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, exception, "Photograph source read failed.");
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
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, exception, "Photograph metadata read failed.");
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
            logger.LogWarning(PhotographLoggerProvider.PipelineEvent, exception, "Photograph sanitisation failed.");
            return null;
        }
    }
}
