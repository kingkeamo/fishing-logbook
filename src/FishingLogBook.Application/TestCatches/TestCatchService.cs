using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.TestCatches;

public sealed class TestCatchService
{
    private readonly ITestCatchRepository _testCatchRepository;

    public TestCatchService(ITestCatchRepository testCatchRepository)
    {
        _testCatchRepository = testCatchRepository;
    }

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
        return ToDto(saved);
    }

    public async Task<IReadOnlyList<TestCatchDto>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await _testCatchRepository.GetAllAsync(cancellationToken);
        return records.Select(ToDto).ToArray();
    }

    private static TestCatchDto ToDto(TestCatchRecord record)
    {
        return new TestCatchDto(record.Id, record.SpeciesName, record.CaughtOn, record.Notes);
    }
}
