using System.ComponentModel;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>
/// Trip and angler selection for a catch being recorded. Mirrors Web Add Catch: the active (or requested)
/// trip is joined automatically, the angler can leave and rejoin it, and a trip that disappears from the
/// device blocks a trip save with an actionable message.
/// </summary>
public sealed class TripAssociationViewModel : INotifyPropertyChanged
{
    private readonly ITripAssociationService _service;
    private readonly UiTextService _text;
    private TripAssociationModel? _trip;
    private TripAssociationModel? _candidate;
    private bool _optedOut;
    private bool _locked;
    private bool _unavailable;
    private Guid _userId;
    private Guid _caughtByUserId;

    public TripAssociationViewModel(ITripAssociationService service, UiTextService text)
    {
        _service = service;
        _text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user changes the trip or angler, so the draft can be saved.</summary>
    public event EventHandler? SelectionChanged;

    public Guid? TripId => _trip?.TripId;

    public string? TripTitle => _trip?.Title;

    public Guid CaughtByUserId => _trip is null ? _userId : _caughtByUserId;

    public bool IsUnavailable => _unavailable;

    public bool ShowAssociation => _trip is not null;

    public bool CanLeave => _trip is not null && !_locked;

    public bool ShowStandalone => _trip is null && _optedOut && !_locked;

    public bool CanRejoin => ShowStandalone && _candidate is not null;

    public string TripName => _trip is null ? string.Empty : Describe(_trip);

    public IReadOnlyList<CatchChipOptionModel> AnglerOptions
    {
        get
        {
            if (_trip is null || _trip.Anglers.Count < 2)
            {
                return [new CatchChipOptionModel("me", _text["CatchAnglerMe"], IsSelected: true)];
            }

            return _trip.Anglers
                .Select(angler => new CatchChipOptionModel(
                    angler.UserId.ToString("D"),
                    angler.IsSelf ? _text["CatchAnglerMe"]
                        : string.IsNullOrWhiteSpace(angler.DisplayName) ? _text["CatchTripAnglerUnknown"] : angler.DisplayName,
                    angler.UserId == _caughtByUserId))
                .ToArray();
        }
    }

    public async Task InitializeNewAsync(Guid userId, Guid? requestedTripId, CancellationToken cancellationToken)
    {
        Reset(userId);
        Associate(await _service.ResolveAsync(requestedTripId, cancellationToken));
    }

    /// <summary>
    /// Restores the trip of a saved or draft record. A draft without a trip stays standalone even when a
    /// trip is now active; the angler can rejoin explicitly.
    /// </summary>
    public async Task RestoreAsync(Guid userId, Guid? tripId, Guid caughtByUserId, bool locked, CancellationToken cancellationToken)
    {
        Reset(userId);
        _locked = locked;
        if (tripId is null)
        {
            var candidate = locked ? null : await _service.ResolveAsync(null, cancellationToken);
            _candidate = candidate;
            _optedOut = candidate is not null;
            Notify();
            return;
        }

        var resolved = await _service.ResolveAsync(tripId, cancellationToken);
        if (resolved is null)
        {
            _trip = new TripAssociationModel(tripId.Value, null, null, default, []);
            _caughtByUserId = userId;
            _unavailable = !locked;
            Notify();
            return;
        }

        _candidate = resolved;
        _trip = resolved;
        _caughtByUserId = resolved.Anglers.Any(angler => angler.UserId == caughtByUserId) ? caughtByUserId : userId;
        Notify();
    }

    public void Leave()
    {
        if (!CanLeave)
        {
            return;
        }

        _candidate = _trip;
        _trip = null;
        _optedOut = true;
        _unavailable = false;
        _caughtByUserId = _userId;
        Notify();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Rejoin()
    {
        if (!CanRejoin)
        {
            return;
        }

        _trip = _candidate;
        _optedOut = false;
        _unavailable = false;
        _caughtByUserId = _userId;
        Notify();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAngler(string code)
    {
        if (_trip is null || _locked || !Guid.TryParse(code, out var userId)
            || _trip.Anglers.All(angler => angler.UserId != userId) || userId == _caughtByUserId)
        {
            return;
        }

        _caughtByUserId = userId;
        Notify();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Confirms the joined trip is still usable on this device; flags it otherwise.</summary>
    public async Task<bool> ConfirmAvailableAsync(CancellationToken cancellationToken)
    {
        if (_trip is null || _locked)
        {
            return true;
        }

        _unavailable = !await _service.IsAvailableAsync(_trip.TripId, cancellationToken);
        Notify();
        return !_unavailable;
    }

    private void Associate(TripAssociationModel? trip)
    {
        _trip = trip;
        _candidate = trip;
        _caughtByUserId = _userId;
        Notify();
    }

    private void Reset(Guid userId)
    {
        _userId = userId;
        _caughtByUserId = userId;
        _trip = null;
        _candidate = null;
        _optedOut = false;
        _locked = false;
        _unavailable = false;
    }

    private string Describe(TripAssociationModel trip)
    {
        if (!string.IsNullOrWhiteSpace(trip.Title))
        {
            return trip.Title;
        }

        if (trip.StartedOn == default)
        {
            return string.Empty;
        }

        var started = trip.StartedOn.ToLocalTime().ToString("g", _text.Culture);
        return string.IsNullOrWhiteSpace(trip.PlaceName) ? started : $"{started} · {trip.PlaceName}";
    }

    private void Notify()
    {
        foreach (var name in new[]
        {
            nameof(TripId), nameof(TripTitle), nameof(CaughtByUserId), nameof(IsUnavailable), nameof(ShowAssociation),
            nameof(CanLeave), nameof(ShowStandalone), nameof(CanRejoin), nameof(TripName), nameof(AnglerOptions)
        })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
