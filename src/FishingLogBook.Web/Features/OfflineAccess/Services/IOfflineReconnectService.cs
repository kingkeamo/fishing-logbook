using FishingLogBook.Web.Features.OfflineAccess.Enums;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public interface IOfflineReconnectService
{
    event EventHandler? StateChanged;

    OfflineReconnectStateEnum State { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task AttemptAsync(CancellationToken cancellationToken);

    void Stop();
}
