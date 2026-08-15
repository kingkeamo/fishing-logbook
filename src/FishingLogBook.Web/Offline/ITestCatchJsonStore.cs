namespace FishingLogBook.Web.Offline;

public interface ITestCatchJsonStore
{
    Task PutAsync(string json, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken);
}
