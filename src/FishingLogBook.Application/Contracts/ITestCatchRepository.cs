using FishingLogBook.Domain.TestCatches;

namespace FishingLogBook.Application.Contracts;

public interface ITestCatchRepository
{
    Task<TestCatchRecord> UpsertAsync(TestCatchRecord record, CancellationToken cancellationToken);

    Task<TestCatchRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestCatchRecord>> GetAllAsync(CancellationToken cancellationToken);

    Task UpsertPhotographAsync(
        Guid testCatchId,
        Guid photographId,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken);
}
