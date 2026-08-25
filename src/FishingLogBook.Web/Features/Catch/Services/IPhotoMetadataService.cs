using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public interface IPhotoMetadataService
{
    Task<PhotoMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        CancellationToken cancellationToken);

    byte[]? Sanitise(byte[] bytes, string contentType);
}
