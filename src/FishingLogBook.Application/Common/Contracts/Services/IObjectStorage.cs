namespace FishingLogBook.Application.Common.Contracts.Services;

public interface IObjectStorage
{
    bool IsConfigured { get; }

    Task<Uri> CreateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);
}
