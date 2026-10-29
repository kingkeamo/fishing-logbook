using FishingLogBook.Mobile.Features.Startup.Enums;

namespace FishingLogBook.Mobile.Features.Startup.Coordinators;

public interface IStartupCoordinator
{
    Task<StartupResultEnum> StartAsync(CancellationToken cancellationToken);
}
