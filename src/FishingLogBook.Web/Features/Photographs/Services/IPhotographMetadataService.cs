using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Features.Photographs.Services;

public interface IPhotographMetadataService
{
    Task<PhotographMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    PhotographHistoricalMetadataModel ReadHistorical(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        DateTimeOffset now);

    byte[]? Sanitise(byte[] bytes, string contentType);
}
