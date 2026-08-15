using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.TestCatches;

public sealed class TestCatchService
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromHours(1);

    private readonly ITestCatchRepository _testCatchRepository;
    private readonly IObjectStorage _objectStorage;

    public TestCatchService(ITestCatchRepository testCatchRepository, IObjectStorage objectStorage)
    {
        _testCatchRepository = testCatchRepository;
        _objectStorage = objectStorage;
    }

    public bool IsObjectStorageConfigured => _objectStorage.IsConfigured;

    public async Task<TestCatchDto> UpsertAsync(TestCatchDto testCatch, CancellationToken cancellationToken)
    {
        var record = new TestCatchRecord
        {
            Id = testCatch.Id,
            SpeciesName = testCatch.SpeciesName,
            CaughtOn = testCatch.CaughtOn,
            Notes = testCatch.Notes
        };

        var saved = await _testCatchRepository.UpsertAsync(record, cancellationToken);
        return await ToDtoAsync(saved, cancellationToken);
    }

    public async Task<IReadOnlyList<TestCatchDto>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await _testCatchRepository.GetAllAsync(cancellationToken);
        var dtos = new List<TestCatchDto>(records.Count);
        foreach (var record in records)
        {
            dtos.Add(await ToDtoAsync(record, cancellationToken));
        }

        return dtos;
    }

    public async Task<PhotographUploadDto?> CreatePhotographUploadAsync(
        Guid testCatchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        var existing = await _testCatchRepository.GetByIdAsync(testCatchId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var objectKey = ObjectKey(testCatchId, request.PhotographId);
        var uploadUrl = await _objectStorage.CreateUploadUrlAsync(
            objectKey,
            request.ContentType,
            UploadLifetime,
            cancellationToken);

        return new PhotographUploadDto(objectKey, uploadUrl.ToString());
    }

    public async Task<bool> RecordPhotographAsync(
        Guid testCatchId,
        RecordPhotographDto request,
        CancellationToken cancellationToken)
    {
        var existing = await _testCatchRepository.GetByIdAsync(testCatchId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (!string.Equals(request.ObjectKey, ObjectKey(testCatchId, request.PhotographId), StringComparison.Ordinal))
        {
            throw new ArgumentException("Photograph object key does not match the catch.", nameof(request));
        }

        await _testCatchRepository.UpsertPhotographAsync(
            testCatchId,
            request.PhotographId,
            request.ObjectKey,
            request.ContentType,
            cancellationToken);

        return true;
    }

    private async Task<TestCatchDto> ToDtoAsync(TestCatchRecord record, CancellationToken cancellationToken)
    {
        string? photographUrl = null;
        if (!string.IsNullOrWhiteSpace(record.PhotographObjectKey) && _objectStorage.IsConfigured)
        {
            var url = await _objectStorage.CreateDownloadUrlAsync(
                record.PhotographObjectKey,
                DownloadLifetime,
                cancellationToken);
            photographUrl = url.ToString();
        }

        return new TestCatchDto(
            record.Id,
            record.SpeciesName,
            record.CaughtOn,
            record.Notes,
            record.PhotographId,
            record.PhotographContentType,
            photographUrl);
    }

    private static string ObjectKey(Guid testCatchId, Guid photographId)
    {
        return $"test-catches/{testCatchId:D}/{photographId:D}";
    }
}
