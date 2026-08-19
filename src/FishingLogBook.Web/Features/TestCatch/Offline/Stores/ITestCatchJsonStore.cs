using FishingLogBook.Web.Features.TestCatch.Models;
namespace FishingLogBook.Web.Features.TestCatch.Offline.Stores;

public interface ITestCatchJsonStore
{
    Task PutAsync(string json, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken);
}
