using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.TestCatches;

namespace FishingLogBook.Application.Tests.TestCatchServiceTests;

internal sealed class MemoryTestCatchRepository : ITestCatchRepository
{
    private readonly Dictionary<Guid, TestCatchRecord> _records = new();

    public Task<TestCatchRecord> UpsertAsync(TestCatchRecord record, CancellationToken cancellationToken)
    {
        _records.TryAdd(record.Id, record);
        return Task.FromResult(_records[record.Id]);
    }

    public Task<IReadOnlyList<TestCatchRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TestCatchRecord>>(_records.Values.ToArray());
    }
}
