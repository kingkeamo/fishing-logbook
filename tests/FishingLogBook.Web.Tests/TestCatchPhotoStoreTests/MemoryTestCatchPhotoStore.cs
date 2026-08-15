using FishingLogBook.Web.Offline;

namespace FishingLogBook.Web.Tests.TestCatchPhotoStoreTests;

internal sealed class MemoryTestCatchPhotoStore : ITestCatchPhotoStore
{
    private readonly Dictionary<Guid, TestCatchPhotoBytes> _items;

    public MemoryTestCatchPhotoStore(Dictionary<Guid, TestCatchPhotoBytes> items)
    {
        _items = items;
    }

    public Task PutAsync(Guid testCatchId, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        _items[testCatchId] = new TestCatchPhotoBytes(bytes, contentType);
        return Task.CompletedTask;
    }

    public Task<TestCatchPhotoBytes?> GetAsync(Guid testCatchId, CancellationToken cancellationToken)
    {
        _items.TryGetValue(testCatchId, out var stored);
        return Task.FromResult(stored);
    }
}
