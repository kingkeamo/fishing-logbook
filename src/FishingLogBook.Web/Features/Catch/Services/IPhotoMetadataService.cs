using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public interface IPhotoMetadataService
{
    Task<PhotoMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken);
}
