using FishingLogBook.Domain.TestCatches;

namespace FishingLogBook.Application.Contracts;

public interface ITestCatchRepository
{
    Task<TestCatchRecord> UpsertAsync(TestCatchRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestCatchRecord>> GetAllAsync(CancellationToken cancellationToken);
}
