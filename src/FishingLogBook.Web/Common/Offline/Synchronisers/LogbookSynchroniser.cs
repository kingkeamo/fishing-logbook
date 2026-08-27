using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

namespace FishingLogBook.Web.Common.Offline.Synchronisers;

public sealed class LogbookSynchroniser : ILogbookSynchroniser
{
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly ITripSynchroniser _tripSynchroniser;
    private readonly ITripPhotographSynchroniser _tripPhotographSynchroniser;
    private readonly ITripNoteSynchroniser _tripNoteSynchroniser;
    private readonly ICatchSynchroniser _catchSynchroniser;
    private readonly ILocalCatchOwnerService _localCatchOwner;
    private readonly ILoggingService _logging;

    public event EventHandler? StateChanged;

    public LogbookSynchroniser(
        ITripSynchroniser tripSynchroniser,
        ITripPhotographSynchroniser tripPhotographSynchroniser,
        ITripNoteSynchroniser tripNoteSynchroniser,
        ICatchSynchroniser catchSynchroniser,
        ILocalCatchOwnerService localCatchOwner,
        ILoggingService logging)
    {
        _tripSynchroniser = tripSynchroniser;
        _tripPhotographSynchroniser = tripPhotographSynchroniser;
        _tripNoteSynchroniser = tripNoteSynchroniser;
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

    public async Task RetryAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var ownerUserId = await _localCatchOwner.GetUserIdAsync(cancellationToken);
        if (ownerUserId != Guid.Empty)
        {
            await RunTripsAsync(ownerUserId, cancellationToken);
        }

        await _catchSynchroniser.RetryAsync(catchId, cancellationToken);
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
        await RunTripsAsync(ownerUserId, cancellationToken);
        await RunTripPhotographsAsync(ownerUserId, cancellationToken);
        await RunTripNotesAsync(ownerUserId, cancellationToken);
        await _catchSynchroniser.SynchronisePendingAsync(ownerUserId, cancellationToken);
    }

    private async Task RunTripNotesAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        try
        {
            await _tripNoteSynchroniser.SynchronisePendingAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync(
                "trip note synchronisation",
                exception,
                CancellationToken.None);
        }
    }

    private async Task RunTripPhotographsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        try
        {
            await _tripPhotographSynchroniser.SynchronisePendingAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync(
                "trip photograph synchronisation",
                exception,
                CancellationToken.None);
        }
    }

    private async Task RunTripsAsync(Guid ownerUserId, CancellationToken cancellationToken)
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
    }
}
