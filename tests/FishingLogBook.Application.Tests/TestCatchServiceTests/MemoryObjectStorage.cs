using FishingLogBook.Application.Contracts;

namespace FishingLogBook.Application.Tests.TestCatchServiceTests;

internal sealed class MemoryObjectStorage : IObjectStorage
{
    public bool IsConfigured { get; init; } = true;

    public Task<Uri> CreateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new Uri($"https://storage.test/upload/{objectKey}"));
    }

    public Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Uri($"https://storage.test/download/{objectKey}"));
    }
}
