using FishingLogBook.Web.Features.TestCatch.Models;
namespace FishingLogBook.Web.Features.TestCatch.Offline.Stores;

public interface ITestCatchStore
{
    Task SaveAsync(TestCatchModel testCatch, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestCatchModel>> GetAllAsync(CancellationToken cancellationToken);
}
