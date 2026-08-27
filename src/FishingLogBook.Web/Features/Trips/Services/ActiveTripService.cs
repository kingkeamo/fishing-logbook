using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Features.Trips.Services;

public sealed class ActiveTripService : IActiveTripService
{
    private static readonly TimeSpan DefaultPlaceTimeout = TimeSpan.FromSeconds(2);

    private readonly ITripStore _tripStore;
    private readonly ILocationService _locationService;
    private readonly IAnglerPreferencesProvider _anglerPreferences;
    private readonly ILoggingService _logging;

    private Guid _cachedOwnerUserId;
    private TripModel? _cachedActiveTrip;
    private bool _hasCachedActiveTrip;

    public event EventHandler? StateChanged;

    public ActiveTripService(
        ITripStore tripStore,
        ILocationService locationService,
        IAnglerPreferencesProvider anglerPreferences,
        ILoggingService logging)
    {
        _tripStore = tripStore;
        _locationService = locationService;
        _anglerPreferences = anglerPreferences;
        _logging = logging;
    }

    public async Task<TripModel?> GetActiveAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            return null;
        }

        if (_hasCachedActiveTrip && _cachedOwnerUserId == ownerUserId)
        {
            return _cachedActiveTrip;
        }

        var active = await _tripStore.GetActiveAsync(ownerUserId, cancellationToken);
        Remember(ownerUserId, active);
        return active;
    }

    public async Task<TripModel> StartAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an owner.");
        }

        var trip = new TripModel(
            Guid.NewGuid(),
            ownerUserId,
            TripConstants.Active,
            DateTimeOffset.UtcNow,
            PlaceName: await TryResolveDefaultPlaceAsync(cancellationToken),
            SyncStatus: SyncStatus.SavedLocally);
        await _tripStore.SaveAsync(trip, cancellationToken);
        Remember(ownerUserId, trip);
        RaiseStateChanged();
        return trip;
    }

    public async Task<TripModel> FinishAsync(TripModel trip, CancellationToken cancellationToken)
    {
        var finished = trip with
        {
            Status = TripConstants.Completed,
            EndedOn = DateTimeOffset.UtcNow,
            SyncStatus = SyncStatus.SavedLocally
        };
        await _tripStore.SaveAsync(finished, cancellationToken);
        Remember(trip.OwnerUserId, null);
        RaiseStateChanged();
        return finished;
    }

    public async Task<TripModel?> UpdatePlaceAsync(
        TripModel trip,
        string? placeName,
        CancellationToken cancellationToken)
    {
        var current = await _tripStore.GetAsync(trip.OwnerUserId, trip.Id, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var updated = current with
        {
            PlaceName = TripConstants.TrimPlaceName(placeName),
            SyncStatus = SyncStatus.SavedLocally
        };
        await _tripStore.SaveAsync(updated, cancellationToken);
        if (updated.Status == TripConstants.Active)
        {
            Remember(trip.OwnerUserId, updated);
        }

        RaiseStateChanged();
        return updated;
    }

    public async Task<TripModel?> TryAttachLocationAsync(TripModel trip, CancellationToken cancellationToken)
    {
        if (trip.Location is not null || trip.Status != TripConstants.Active)
        {
            return null;
        }

        var captured = await TryCaptureAsync(cancellationToken);
        if (captured is null)
        {
            return null;
        }

        var current = await _tripStore.GetAsync(trip.OwnerUserId, trip.Id, cancellationToken);
        if (current is null || current.Status != TripConstants.Active || current.Location is not null)
        {
            return null;
        }

        var located = current with { Location = captured };
        await _tripStore.SaveAsync(located, cancellationToken);
        Remember(trip.OwnerUserId, located);
        return located;
    }

    public void Invalidate()
    {
        _hasCachedActiveTrip = false;
        _cachedActiveTrip = null;
        _cachedOwnerUserId = Guid.Empty;
    }

    private async Task<string?> TryResolveDefaultPlaceAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultPlaceTimeout);
        try
        {
            var preferences = await _anglerPreferences.GetAsync(timeout.Token);
            return TripConstants.TrimPlaceName(preferences.DefaultLocation?.Name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync(
                "resolving the default fishing location",
                exception,
                CancellationToken.None);
            return null;
        }
    }

    private async Task<TripLocationModel?> TryCaptureAsync(CancellationToken cancellationToken)
    {
        try
        {
            var captured = await _locationService.TryCaptureAsync(userRequested: false, cancellationToken);
            if (captured is null)
            {
                return null;
            }

            return new TripLocationModel(
                captured.Latitude,
                captured.Longitude,
                captured.AccuracyMetres,
                captured.CapturedOn,
                captured.Source,
                captured.Visibility,
                captured.ConsentVersion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _logging.LogErrorAsync("capturing a trip location", exception, CancellationToken.None);
            return null;
        }
    }

    private void Remember(Guid ownerUserId, TripModel? trip)
    {
        _cachedOwnerUserId = ownerUserId;
        _cachedActiveTrip = trip;
        _hasCachedActiveTrip = true;
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
