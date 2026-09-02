using FishingLogBook.Domain.SystemStatus;

namespace FishingLogBook.Application.SystemStatus.Contracts.Repositories;

public interface ISystemRepository
{
    Task<SystemHealth?> GetSystemHealthAsync(CancellationToken cancellationToken);
}
