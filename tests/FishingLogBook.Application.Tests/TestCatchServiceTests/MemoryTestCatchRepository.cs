using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.TestCatches;

namespace FishingLogBook.Application.Tests.TestCatchServiceTests;

internal sealed class MemoryTestCatchRepository : ITestCatchRepository
{
    private readonly Dictionary<Guid, TestCatchRecord> _records = new();

    public Task<TestCatchRecord> UpsertAsync(TestCatchRecord record, CancellationToken cancellationToken)
    {
        if (_records.TryGetValue(record.Id, out var existing))
        {
            _records[record.Id] = new TestCatchRecord
            {
                Id = existing.Id,
                SpeciesName = existing.SpeciesName,
                CaughtOn = existing.CaughtOn,
                Notes = existing.Notes,
                Latitude = record.Latitude,
                Longitude = record.Longitude,
                LocationAccuracyMetres = record.LocationAccuracyMetres,
                LocationCapturedOn = record.LocationCapturedOn,
                LocationSource = record.LocationSource,
                LocationVisibility = record.LocationVisibility,
                LocationConsentVersion = record.LocationConsentVersion,
                PhotographId = existing.PhotographId,
                PhotographObjectKey = existing.PhotographObjectKey,
                PhotographContentType = existing.PhotographContentType
            };
        }
        else
        {
            _records[record.Id] = record;
        }

        return Task.FromResult(_records[record.Id]);
    }

    public Task<TestCatchRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<TestCatchRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TestCatchRecord>>(_records.Values.ToArray());
    }

    public Task UpsertPhotographAsync(
        Guid testCatchId,
        Guid photographId,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken)
    {
        var existing = _records[testCatchId];
        _records[testCatchId] = new TestCatchRecord
        {
            Id = existing.Id,
            SpeciesName = existing.SpeciesName,
            CaughtOn = existing.CaughtOn,
            Notes = existing.Notes,
            Latitude = existing.Latitude,
            Longitude = existing.Longitude,
            LocationAccuracyMetres = existing.LocationAccuracyMetres,
            LocationCapturedOn = existing.LocationCapturedOn,
            LocationSource = existing.LocationSource,
            LocationVisibility = existing.LocationVisibility,
            LocationConsentVersion = existing.LocationConsentVersion,
            PhotographId = photographId,
            PhotographObjectKey = objectKey,
            PhotographContentType = contentType
        };

        return Task.CompletedTask;
    }
}
