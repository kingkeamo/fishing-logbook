using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Offline.TestCatchStoreTests;

internal sealed class MemoryTestCatchJsonStore : ITestCatchJsonStore
{
    private readonly List<string> _items;

    public MemoryTestCatchJsonStore(List<string> items)
    {
        _items = items;
    }

    public Task PutAsync(string json, CancellationToken cancellationToken)
    {
        _items.Add(json);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>(_items.ToArray());
    }
}
