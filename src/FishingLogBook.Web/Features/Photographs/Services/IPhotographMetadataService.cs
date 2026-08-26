using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Features.Photographs.Services;

public interface IPhotographMetadataService
{
    Task<PhotographMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        CancellationToken cancellationToken);

    byte[]? Sanitise(byte[] bytes, string contentType);
}
