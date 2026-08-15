using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Services;

public interface ITestCatchClient
{
    Task UpsertAsync(TestCatchDto testCatch, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestCatchDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<PhotographUploadDto> CreatePhotographUploadAsync(
        Guid testCatchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken);

    Task UploadPhotographAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken cancellationToken);

    Task RecordPhotographAsync(Guid testCatchId, RecordPhotographDto request, CancellationToken cancellationToken);
}
