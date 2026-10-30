using FishingLogBook.Shared.Photographs.Models;
using FishingLogBook.Shared.Photographs.Services;

namespace FishingLogBook.Web.Features.Photographs.Services;

public sealed class PhotographMetadataService(PhotographMetadataProcessor processor) : IPhotographMetadataService
{
    public Task<PhotographMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return processor.ReadAsync(bytes, contentType, fileLastModified, now, cancellationToken);
    }

    public PhotographHistoricalMetadataModel ReadHistorical(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        DateTimeOffset now)
    {
        return processor.ReadHistorical(bytes, contentType, fileLastModified, now);
    }

    public byte[]? Sanitise(byte[] bytes, string contentType)
    {
        return processor.Sanitise(bytes, contentType);
    }
}
