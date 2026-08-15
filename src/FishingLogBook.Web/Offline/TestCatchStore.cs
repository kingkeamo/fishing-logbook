using System.Text.Json;

namespace FishingLogBook.Web.Offline;

public sealed class TestCatchStore : ITestCatchStore
{
    private readonly ITestCatchJsonStore _jsonStore;

    public TestCatchStore(ITestCatchJsonStore jsonStore)
    {
        _jsonStore = jsonStore;
    }

    public Task SaveAsync(TestCatch testCatch, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(testCatch, TestCatchJson.Options);
        return _jsonStore.PutAsync(json, cancellationToken);
    }

    public async Task<IReadOnlyList<TestCatch>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await _jsonStore.GetAllAsync(cancellationToken);
        return items
            .Select(json => JsonSerializer.Deserialize<TestCatch>(json, TestCatchJson.Options))
            .Where(testCatch => testCatch is not null)
            .Cast<TestCatch>()
            .OrderByDescending(testCatch => testCatch.CaughtOn)
            .ToArray();
    }
}
