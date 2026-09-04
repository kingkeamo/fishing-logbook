using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportPhotoBlobRegistryService
{
    Task<ImportPhotoBlobRegistrationModel> RegisterAsync(
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken);

    Task<byte[]> GetBytesAsync(string token, CancellationToken cancellationToken);

    Task RemoveAsync(string token, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
