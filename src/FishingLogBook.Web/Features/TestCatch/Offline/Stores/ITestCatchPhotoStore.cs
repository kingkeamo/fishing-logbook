using FishingLogBook.Web.Features.TestCatch.Models;
namespace FishingLogBook.Web.Features.TestCatch.Offline.Stores;

public interface ITestCatchPhotoStore
{
    Task PutAsync(Guid testCatchId, byte[] bytes, string contentType, CancellationToken cancellationToken);

    Task<TestCatchPhotoBytesModel?> GetAsync(Guid testCatchId, CancellationToken cancellationToken);
}
