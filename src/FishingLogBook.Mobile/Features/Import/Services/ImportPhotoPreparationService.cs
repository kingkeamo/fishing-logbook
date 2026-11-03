using System.Security.Cryptography;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Photographs.Constants;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Enums;
using FishingLogBook.Shared.Photographs.Models;
using FishingLogBook.Shared.Photographs.Services;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Import.Services;

public sealed class ImportPhotoPreparationService : IImportPhotoPreparationService
{
    public const int MaxPhotographs = 20;
    public const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly PhotographMetadataProcessor _metadata;
    private readonly IImportPhotoBlobRegistryService _registry;
    private readonly IFileSystemAdapter _files;
    private readonly TimeProvider _clock;
    private readonly ILogger<ImportPhotoPreparationService> _logger;

    public ImportPhotoPreparationService(
        PhotographMetadataProcessor metadata,
        IImportPhotoBlobRegistryService registry,
        IFileSystemAdapter files,
        TimeProvider clock,
        ILogger<ImportPhotoPreparationService> logger)
    {
        _metadata = metadata;
        _registry = registry;
        _files = files;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportSelectedPhotoModel>> PrepareSelectionAsync(
        IReadOnlyList<PhotographPickedFileModel> files,
        CancellationToken cancellationToken)
    {
        if (files.Count > MaxPhotographs)
        {
            throw new ArgumentOutOfRangeException(nameof(files), $"At most {MaxPhotographs} photographs may be selected.");
        }

        await _registry.ClearAsync(cancellationToken);
        var prepared = new List<ImportSelectedPhotoModel>(files.Count);
        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                prepared.Add(await PrepareAsync(files[index], index, cancellationToken));
            }

            DetectDuplicates(prepared);
            return prepared;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            foreach (var photo in prepared)
            {
                photo.SetPreparation(ImportPhotoPreparationStatusEnum.Cancelled);
            }

            await ClearWithoutCancellationAsync();
            throw;
        }
    }

    public async Task RemoveAsync(ImportSelectedPhotoModel photo, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(photo.BlobToken))
        {
            await _registry.RemoveAsync(photo.BlobToken, cancellationToken);
        }

        photo.Remove();
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _registry.ClearAsync(cancellationToken);
    }

    private async Task<ImportSelectedPhotoModel> PrepareAsync(
        PhotographPickedFileModel file,
        int selectionIndex,
        CancellationToken cancellationToken)
    {
        var size = ReadSize(file.FullPath);
        var photo = new ImportSelectedPhotoModel(
            Guid.NewGuid(),
            selectionIndex,
            file.ContentType,
            size,
            null,
            Path.GetFileName(file.FullPath));
        if (PhotographHeicContentTypeConstants.IsHeic(file.ContentType)
            || !PhotographContentTypeConstants.IsAllowed(file.ContentType))
        {
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.UnsupportedType);
            return photo;
        }

        if (size > MaxPhotographBytes)
        {
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.TooLarge);
            return photo;
        }

        try
        {
            var original = ReadBytes(file.FullPath);
            if (original is null)
            {
                photo.SetPreparation(ImportPhotoPreparationStatusEnum.PreparationFailed);
                return photo;
            }

            var historical = ReadMetadata(original, file);
            if (historical is null)
            {
                photo.SetMetadata(
                    ImportMetadataStatusEnum.Failed,
                    ImportTimestampModel.Missing(),
                    new ImportLocationModel(null, null, false),
                    "metadata-unavailable");
            }
            else
            {
                photo.SetMetadata(
                    historical.CapturedOnWasPresent || historical.HasCoordinates
                        ? ImportMetadataStatusEnum.Available
                        : ImportMetadataStatusEnum.Unavailable,
                    MapTimestamp(historical),
                    MapLocation(historical));
            }

            var sanitised = _metadata.Sanitise(original, file.ContentType);
            if (sanitised is null)
            {
                photo.SetPreparation(ImportPhotoPreparationStatusEnum.PreparationFailed);
                return photo;
            }

            photo.SetFingerprint(Convert.ToHexString(SHA256.HashData(sanitised)));
            cancellationToken.ThrowIfCancellationRequested();
            var registration = await _registry.RegisterAsync(sanitised, file.ContentType, cancellationToken);
            photo.SetPreparation(
                ImportPhotoPreparationStatusEnum.Ready,
                registration.Token,
                registration.ThumbnailUrl);
            return photo;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.Cancelled);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "A historical photograph could not be prepared.");
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.PreparationFailed);
            return photo;
        }
    }

    private static void DetectDuplicates(IReadOnlyList<ImportSelectedPhotoModel> photos)
    {
        var ready = photos.Where(photo => photo.IsReady).OrderBy(photo => photo.SelectionIndex).ToArray();
        foreach (var group in ready
                     .Where(photo => !string.IsNullOrWhiteSpace(photo.Fingerprint))
                     .GroupBy(photo => photo.Fingerprint, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            var members = group.ToArray();
            foreach (var duplicate in members.Skip(1))
            {
                duplicate.SetDuplicateState(
                    ImportDuplicateStatusEnum.Duplicate,
                    ImportDuplicateReasonEnum.IdenticalPreparedBytes,
                    members.Where(photo => photo.Id != duplicate.Id).Select(photo => photo.Id).ToArray());
            }
        }

        foreach (var photo in ready.Where(photo => photo.DuplicateStatus != ImportDuplicateStatusEnum.Duplicate))
        {
            var counterpart = ready.FirstOrDefault(candidate =>
                candidate.SelectionIndex < photo.SelectionIndex
                && candidate.Fingerprint != photo.Fingerprint
                && HasSameCaptureTime(photo, candidate)
                && candidate.ByteSize == photo.ByteSize);
            if (counterpart is not null)
            {
                photo.SetDuplicateState(
                    ImportDuplicateStatusEnum.Warning,
                    ImportDuplicateReasonEnum.SameCaptureTimeAndSize,
                    [counterpart.Id]);
            }
            else
            {
                photo.SetDuplicateState(ImportDuplicateStatusEnum.None, ImportDuplicateReasonEnum.None, []);
            }
        }
    }

    private static bool HasSameCaptureTime(ImportSelectedPhotoModel first, ImportSelectedPhotoModel second)
    {
        if (first.Timestamp.State == ImportTimestampStateEnum.ExplicitInstant
            && second.Timestamp.State == ImportTimestampStateEnum.ExplicitInstant)
        {
            return first.Timestamp.Instant == second.Timestamp.Instant;
        }

        return first.Timestamp.LocalWallClock.HasValue
            && first.Timestamp.LocalWallClock == second.Timestamp.LocalWallClock;
    }

    private PhotographHistoricalMetadataModel? ReadMetadata(byte[] original, PhotographPickedFileModel file)
    {
        try
        {
            return _metadata.ReadHistorical(
                original,
                file.ContentType,
                file.LastModified,
                _clock.GetUtcNow());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Historical photograph metadata could not be read.");
            return null;
        }
    }

    private static ImportTimestampModel MapTimestamp(PhotographHistoricalMetadataModel metadata)
    {
        if (metadata.CapturedOnWasMalformed)
        {
            return ImportTimestampModel.Unusable(ToImportSource(metadata.CapturedOnSource));
        }

        if (metadata.CapturedOnSource == PhotographCapturedOnSourceEnum.FileLastModified
            && metadata.ExplicitInstant is { } fallback)
        {
            return ImportTimestampModel.FromWeakFallback(fallback);
        }

        if (metadata.ExplicitInstant is { } instant)
        {
            return ImportTimestampModel.FromExplicitInstant(instant, ToImportSource(metadata.CapturedOnSource));
        }

        if (metadata.LocalWallClock is { } wallClock)
        {
            return ImportTimestampModel.FromLocalWallClock(wallClock, ToImportSource(metadata.CapturedOnSource));
        }

        return ImportTimestampModel.Missing();
    }

    private static ImportTimestampSourceEnum ToImportSource(PhotographCapturedOnSourceEnum source)
    {
        return source switch
        {
            PhotographCapturedOnSourceEnum.ExifOriginal => ImportTimestampSourceEnum.ExifOriginal,
            PhotographCapturedOnSourceEnum.ExifDigitized => ImportTimestampSourceEnum.ExifDigitized,
            PhotographCapturedOnSourceEnum.FileLastModified => ImportTimestampSourceEnum.FileLastModified,
            _ => ImportTimestampSourceEnum.None
        };
    }

    private static ImportLocationModel MapLocation(PhotographHistoricalMetadataModel metadata)
    {
        return metadata.HasCoordinates
            ? new ImportLocationModel(metadata.Latitude, metadata.Longitude, true)
            : new ImportLocationModel(null, null, false);
    }

    private long ReadSize(string path)
    {
        if (!_files.FileExists(path))
        {
            return 0;
        }

        using var stream = _files.OpenRead(path);
        return stream.Length;
    }

    private byte[]? ReadBytes(string path)
    {
        if (!_files.FileExists(path))
        {
            return null;
        }

        using var stream = _files.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private async Task ClearWithoutCancellationAsync()
    {
        try
        {
            await _registry.ClearAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Prepared Import photographs could not be cleared.");
        }
    }
}
