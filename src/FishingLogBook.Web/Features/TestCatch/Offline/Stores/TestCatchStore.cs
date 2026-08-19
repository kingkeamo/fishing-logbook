using System.Text.Json;
using FishingLogBook.Web.Features.TestCatch.Models;

namespace FishingLogBook.Web.Features.TestCatch.Offline.Stores;

public sealed class TestCatchStore : ITestCatchStore
{
    private readonly ITestCatchJsonStore _jsonStore;

    public TestCatchStore(ITestCatchJsonStore jsonStore)
    {
        _jsonStore = jsonStore;
    }

    public Task SaveAsync(TestCatchModel testCatch, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(testCatch, TestCatchJson.Options);
        return _jsonStore.PutAsync(json, cancellationToken);
    }

    public async Task<IReadOnlyList<TestCatchModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await _jsonStore.GetAllAsync(cancellationToken);
        return items
            .Select(json => JsonSerializer.Deserialize<TestCatchModel>(json, TestCatchJson.Options))
            .Where(testCatch => testCatch is not null)
            .Cast<TestCatchModel>()
            .OrderByDescending(testCatch => testCatch.CaughtOn)
            .ToArray();
    }
}
