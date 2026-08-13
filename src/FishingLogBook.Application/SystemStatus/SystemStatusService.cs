using FishingLogBook.Application.Contracts;
using FishingLogBook.Shared.SystemStatus;

namespace FishingLogBook.Application.SystemStatus;

public sealed class SystemStatusService
{
    private readonly ISystemRepository _systemRepository;

    public SystemStatusService(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<DatabaseTestResponse> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        var record = await _systemRepository.GetSystemTestRecordAsync(cancellationToken);

        if (record is null)
        {
            return new DatabaseTestResponse("Degraded", null);
        }

        return new DatabaseTestResponse("Healthy", record.Name);
    }
}
