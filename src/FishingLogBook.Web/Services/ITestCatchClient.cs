using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Services;

public interface ITestCatchClient
{
    Task UpsertAsync(TestCatchDto testCatch, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestCatchDto>> GetAllAsync(CancellationToken cancellationToken);
}
