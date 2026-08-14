namespace FishingLogBook.Application.Contracts;

public interface IObjectStorage
{
    bool IsConfigured { get; }

    Task<Uri> CreateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken);
}
