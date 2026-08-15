namespace FishingLogBook.Web.Offline;

public interface ITestCatchPhotoStore
{
    Task PutAsync(Guid testCatchId, byte[] bytes, string contentType, CancellationToken cancellationToken);

    Task<TestCatchPhotoBytes?> GetAsync(Guid testCatchId, CancellationToken cancellationToken);
}
