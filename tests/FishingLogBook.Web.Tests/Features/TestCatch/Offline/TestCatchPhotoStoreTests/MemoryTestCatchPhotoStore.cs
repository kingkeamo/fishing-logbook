using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Offline.TestCatchPhotoStoreTests;

internal sealed class MemoryTestCatchPhotoStore : ITestCatchPhotoStore
{
    private readonly Dictionary<Guid, TestCatchPhotoBytesModel> _items;

    public MemoryTestCatchPhotoStore(Dictionary<Guid, TestCatchPhotoBytesModel> items)
    {
        _items = items;
    }

    public Task PutAsync(Guid testCatchId, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        _items[testCatchId] = new TestCatchPhotoBytesModel(bytes, contentType);
        return Task.CompletedTask;
    }

    public Task<TestCatchPhotoBytesModel?> GetAsync(Guid testCatchId, CancellationToken cancellationToken)
    {
        _items.TryGetValue(testCatchId, out var stored);
        return Task.FromResult(stored);
    }
}
