using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>
/// The "fishing trip in progress" state shared by every main page, like the Web layout banner: the angler's
/// own active trip, with the trip actions the Catches page offers (update the active trip, or start one).
/// </summary>
public sealed class ActiveTripBannerViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(20);

    private readonly ITripService _trips;
    private readonly IAnglerPreferencesService _preferences;
    private readonly NavigationService _navigation;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Guid? _activeTripId;
    private bool _isLoaded;
    private bool _isBusy;
    private DateTimeOffset _refreshedOn = DateTimeOffset.MinValue;

    public ActiveTripBannerViewModel(
        ITripService trips,
        IAnglerPreferencesService preferences,
        IAccountSessionCoordinator session,
        NavigationService navigation,
        UiTextService text,
        TimeProvider clock)
    {
        _navigation = navigation;
        _trips = trips;
        _preferences = preferences;
        _clock = clock;
        Text = text;
        text.PropertyChanged += (_, _) => Notify();
        session.SessionActivated += async (_, _) => await RefreshAsync(force: true, CancellationToken.None);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public Guid? ActiveTripId => _activeTripId;

    public bool HasActiveTrip => _activeTripId is not null;

    public bool CanStart => _isLoaded && !HasActiveTrip && !_isBusy;

    public bool ShowAction => CanStart;

    public string ActionText => Text["TripStartFishing"];

    public async Task RefreshAsync(bool force, CancellationToken cancellationToken)
    {
        if (!force && _isLoaded && _clock.GetUtcNow() - _refreshedOn < RefreshInterval)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var history = await _trips.LoadAsync(cancellationToken);
            _activeTripId = history.Items
                .FirstOrDefault(trip => trip.Status == TripConstants.Active && trip.Role == TripParticipantConstants.Owner)?.Id;
            _isLoaded = true;
            _refreshedOn = _clock.GetUtcNow();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _activeTripId = null;
            _isLoaded = false;
        }
        finally
        {
            _gate.Release();
            Notify();
        }
    }

    public async Task ViewTripAsync()
    {
        if (_activeTripId is { } tripId)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.TripDetail, tripId: tripId);
        }
    }

    public async Task UpdateTripAsync()
    {
        if (_activeTripId is { } tripId)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.TripEdit, tripId: tripId);
        }
    }

    /// <summary>The Catches page action: start fishing and open the new trip. An active trip is updated from the banner instead.</summary>
    public async Task StartAndOpenAsync(CancellationToken cancellationToken)
    {
        if (await StartAsync(cancellationToken) is { } started)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.TripDetail, tripId: started);
        }
    }

    public async Task<Guid?> StartAsync(CancellationToken cancellationToken)
    {
        if (!CanStart)
        {
            return null;
        }

        _isBusy = true;
        Notify();
        try
        {
            var preferences = await _preferences.GetCachedAsync(cancellationToken);
            var place = preferences?.Locations.Locations.FirstOrDefault(location => location.IsDefault)?.Name;
            var trip = await _trips.StartAsync(null, place, cancellationToken);
            _activeTripId = trip.Detail.Trip.Id;
            return _activeTripId;
        }
        catch (InvalidOperationException)
        {
            _isBusy = false;
            await RefreshAsync(force: true, cancellationToken);
            return null;
        }
        finally
        {
            _isBusy = false;
            Notify();
        }
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
