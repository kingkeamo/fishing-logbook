using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.TestCatch.Models;

namespace FishingLogBook.Web.Features.TestCatch.Services;

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
