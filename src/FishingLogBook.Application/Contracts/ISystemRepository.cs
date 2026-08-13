using FishingLogBook.Domain.SystemStatus;

namespace FishingLogBook.Application.Contracts;

public interface ISystemRepository
{
    Task<SystemTestRecord?> GetSystemTestRecordAsync(CancellationToken cancellationToken);
}
