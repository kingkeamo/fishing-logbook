namespace FishingLogBook.Web.Offline;

public interface ITestCatchStore
{
    Task SaveAsync(TestCatch testCatch, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestCatch>> GetAllAsync(CancellationToken cancellationToken);
}
