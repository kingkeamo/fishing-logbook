using FishingLogBook.Web.Features.TestCatch.Models;
namespace FishingLogBook.Web.Features.TestCatch.Offline;

public interface ITestCatchJsonStore
{
    Task PutAsync(string json, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken);
}
