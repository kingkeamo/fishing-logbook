using FishingLogBook.Application.Contracts;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.SystemStatus;

public sealed class SystemStatusService
{
    private readonly ISystemRepository _systemRepository;

    public SystemStatusService(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<DatabaseTestDto> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        var record = await _systemRepository.GetSystemTestRecordAsync(cancellationToken);

        if (record is null)
        {
            return new DatabaseTestDto("Degraded", null);
        }

        return new DatabaseTestDto("Healthy", record.Name);
    }
}
