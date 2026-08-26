using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FishingLogBook.Web.Features.Photographs.Services;

public sealed class PhotographPreparationService : IPhotographPreparationService
{
    public const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly IPhotographMetadataService _metadata;
    private readonly ITimeService _time;
    private readonly ILoggingService _logging;

    public PhotographPreparationService(
        IPhotographMetadataService metadata,
        ITimeService time,
        ILoggingService logging)
    {
        _metadata = metadata;
        _time = time;
        _logging = logging;
    }

    public async Task<PhotographPreparationModel> PrepareAsync(
        IBrowserFile file,
        PhotographSourceEnum source,
        CancellationToken cancellationToken)
    {
        var contentType = file.ContentType;
        if (!PhotographContentTypeConstants.IsAllowed(contentType))
        {
            return PhotographPreparationModel.Unsupported;
        }

        var original = await ReadBytesAsync(file, cancellationToken);
        if (original is null)
        {
            return PhotographPreparationModel.CouldNotPrepare;
        }

        var metadata = source == PhotographSourceEnum.Camera
            ? PhotographMetadataModel.Empty
            : await ReadMetadataAsync(original, contentType, file.LastModified, cancellationToken);
        var sanitised = await SanitiseAsync(original, contentType);
        if (sanitised is null)
        {
            return PhotographPreparationModel.CouldNotPrepare;
        }

        return PhotographPreparationModel.Prepared(new PreparedPhotographModel(
            Guid.NewGuid(),
            contentType,
            sanitised,
            source,
            metadata,
            await ToLocalValueAsync(metadata.CapturedOn, cancellationToken)));
    }

    private async Task<byte[]?> ReadBytesAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream(MaxPhotographBytes);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await LogSafelyAsync("reading a selected photograph", "A photograph could not be read", exception);
            return null;
        }
    }

    private async Task<PhotographMetadataModel> ReadMetadataAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset fileLastModified,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _metadata.ReadAsync(bytes, contentType, fileLastModified, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await LogSafelyAsync("reading photograph metadata", "Photograph metadata could not be read", exception);
            return PhotographMetadataModel.Empty;
        }
    }

    private async Task<byte[]?> SanitiseAsync(byte[] bytes, string contentType)
    {
        try
        {
            return _metadata.Sanitise(bytes, contentType);
        }
        catch (Exception exception)
        {
            await LogSafelyAsync("removing photograph metadata", "Photograph metadata could not be removed", exception);
            return null;
        }
    }

    private async Task<string?> ToLocalValueAsync(DateTimeOffset? instant, CancellationToken cancellationToken)
    {
        if (instant is null)
        {
            return null;
        }

        try
        {
            return await _time.ToDateTimeLocalValueAsync(instant.Value, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Task LogSafelyAsync(string source, string message, Exception exception)
    {
        return _logging.LogErrorAsync(
            source,
            $"{message} ({exception.GetType().Name}).",
            CancellationToken.None);
    }
}
