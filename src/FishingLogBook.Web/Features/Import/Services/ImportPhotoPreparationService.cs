using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportPhotoPreparationService : IImportPhotoPreparationService
{
    public const int MaxPhotographs = 20;
    public const long MaxPhotographBytes = PhotographPreparationService.MaxPhotographBytes;

    private readonly IPhotographMetadataService _metadata;
    private readonly IImportPhotoBlobRegistryService _registry;
    private readonly ILoggingService _logging;

    public ImportPhotoPreparationService(
        IPhotographMetadataService metadata,
        IImportPhotoBlobRegistryService registry,
        ILoggingService logging)
    {
        _metadata = metadata;
        _registry = registry;
        _logging = logging;
    }

    public async Task<IReadOnlyList<ImportSelectedPhotoModel>> PrepareSelectionAsync(
        IReadOnlyList<IBrowserFile> files,
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
        IBrowserFile file,
        int selectionIndex,
        CancellationToken cancellationToken)
    {
        var photo = NewPhoto(file, selectionIndex);
        if (!PhotographContentTypeConstants.IsAllowed(file.ContentType))
        {
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.UnsupportedType);
            return photo;
        }

        if (file.Size > MaxPhotographBytes)
        {
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.TooLarge);
            return photo;
        }

        try
        {
            var original = await ReadBytesAsync(file, cancellationToken);
            var historical = await ReadMetadataAsync(original, file);
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
            await LogSafelyAsync(exception);
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.PreparationFailed);
            return photo;
        }
    }

    private static ImportSelectedPhotoModel NewPhoto(IBrowserFile file, int selectionIndex)
    {
        return new ImportSelectedPhotoModel(
            Guid.NewGuid(),
            selectionIndex,
            file.ContentType,
            file.Size,
            null,
            file.Name);
    }

    private static async Task<byte[]> ReadBytesAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream(MaxPhotographBytes, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private async Task<PhotographHistoricalMetadataModel?> ReadMetadataAsync(
        byte[] original,
        IBrowserFile file)
    {
        try
        {
            return _metadata.ReadHistorical(
                original,
                file.ContentType,
                file.LastModified,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            await LogSafelyAsync(exception);
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

    private Task LogSafelyAsync(Exception exception)
    {
        return _logging.LogErrorAsync(
            "preparing a historical photograph",
            $"A historical photograph could not be prepared ({exception.GetType().Name}).",
            CancellationToken.None);
    }

    private async Task ClearWithoutCancellationAsync()
    {
        try
        {
            await _registry.ClearAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            await LogSafelyAsync(exception);
        }
    }
}
