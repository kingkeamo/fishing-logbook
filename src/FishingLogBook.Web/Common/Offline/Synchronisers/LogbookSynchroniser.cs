using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

namespace FishingLogBook.Web.Common.Offline.Synchronisers;

public sealed class LogbookSynchroniser : ILogbookSynchroniser
{
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly ITripSynchroniser _tripSynchroniser;
    private readonly ICatchSynchroniser _catchSynchroniser;
    private readonly ILocalCatchOwnerService _localCatchOwner;
    private readonly ILoggingService _logging;

    public event EventHandler? StateChanged;

    public LogbookSynchroniser(
        ITripSynchroniser tripSynchroniser,
        ICatchSynchroniser catchSynchroniser,
        ILocalCatchOwnerService localCatchOwner,
        ILoggingService logging)
    {
        _tripSynchroniser = tripSynchroniser;
        _catchSynchroniser = catchSynchroniser;
        _localCatchOwner = localCatchOwner;
        _logging = logging;
    }

    public async Task SynchronisePendingAsync(CancellationToken cancellationToken)
    {
        var ownerUserId = await _localCatchOwner.GetUserIdAsync(cancellationToken);
        await SynchronisePendingAsync(ownerUserId, cancellationToken);
    }

    public async Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            await RunTripsThenCatchesAsync(ownerUserId, cancellationToken);
        }
        finally
        {
            _runLock.Release();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task CleanupSyncedCacheAsync(CancellationToken cancellationToken)
    {
        var ownerUserId = await _localCatchOwner.GetUserIdAsync(cancellationToken);
        await CleanupSyncedCacheAsync(ownerUserId, cancellationToken);
    }

    public async Task CleanupSyncedCacheAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        await _tripSynchroniser.CleanupSyncedCacheAsync(ownerUserId, cancellationToken);
        await _catchSynchroniser.CleanupSyncedCacheAsync(ownerUserId, cancellationToken);
    }

    private async Task RunTripsThenCatchesAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        try
        {
            await _tripSynchroniser.SynchronisePendingAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("trip synchronisation", exception, CancellationToken.None);
        }

        await _catchSynchroniser.SynchronisePendingAsync(ownerUserId, cancellationToken);
    }
}
