using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>
/// The plan-a-trip screen: creates, edits and cancels a planned trip. Every change is written to SQLite first and
/// synchronised later; inviting needs the trip to exist on the server, so it is offered once the plan has synchronised.
/// </summary>
public sealed class TripPlanViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IPlannedTripService _planned;
    private readonly ITripService _trips;
    private readonly IOutboxChangeNotifier _notifier;
    private readonly IAnglerPreferencesService _preferences;
    private readonly IAccountSessionCoordinator _session;
    private readonly IConnectivityService _connectivity;
    private readonly InteractionService _interactions;
    private readonly TimeProvider _clock;
    private readonly ILogger<TripPlanViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private LocalTripRecordModel? _trip;
    private int _rereadGeneration;
    private volatile bool _disposed;
    private IReadOnlyList<string> _savedLocations = [];
    private bool _isLoading = true;
    private bool _notFound;
    private bool _isBusy;
    private string _editTitle = string.Empty;
    private string _editPlace = string.Empty;
    private DateTime _plannedDate;
    private TimeSpan _plannedTime;
    private string? _messageKey;

    public TripPlanViewModel(
        IPlannedTripService planned,
        ITripService trips,
        IOutboxChangeNotifier notifier,
        IAnglerPreferencesService preferences,
        IAccountSessionCoordinator session,
        IConnectivityService connectivity,
        InteractionService interactions,
        UiTextService text,
        TimeProvider clock,
        ILogger<TripPlanViewModel> logger,
        SynchronizationContext? uiContext = null)
    {
        _planned = planned;
        _trips = trips;
        _notifier = notifier;
        _preferences = preferences;
        _session = session;
        _connectivity = connectivity;
        _interactions = interactions;
        _clock = clock;
        _logger = logger;
        _uiContext = uiContext ?? SynchronizationContext.Current;
        Text = text;
        text.PropertyChanged += (_, _) => Notify(string.Empty);
        _notifier.SyncRunCompleted += OnSyncRunCompleted;
        SetDefaultStart();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<CatchChipOptionModel> LocationOptions { get; } = [];

    public LocalTripRecordModel? Trip => _trip;

    public Guid? TripId => _trip?.Detail.Trip.Id;

    public bool IsNew => _trip is null;

    public bool IsLoading => _isLoading;

    public bool NotFound => _notFound;

    public bool ShowContent => !_isLoading && !_notFound;

    public bool CanInteract => !_isLoading && !_isBusy;

    public bool IsCancelled => _trip is { IsCancelled: true };

    public bool IsReadOnly => _trip is not null && !CanEdit;

    public bool CanEdit => _trip is null || _trip is { IsPlanned: true, CanManage: true };

    public bool CanSave => CanEdit && CanInteract;

    public bool CanCancelTrip => _trip is { IsPlanned: true, CanManage: true } && !_isBusy;

    public bool CanInvite => CanCancelTrip && _connectivity.IsOnline && _trip is { SyncedRevision: > 0 };

    public bool ShowInviteHint => CanCancelTrip && !CanInvite;

    public bool HasPendingSync => _trip is { HasPendingMetadata: true };

    public string HeadingText => Text[_trip is null ? "TripPlanTitle" : "TripPlanEditTitle"];

    public bool HasLocationOptions => LocationOptions.Count > 0;

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public string EditTitle
    {
        get => _editTitle;
        set
        {
            if (_editTitle != value)
            {
                _editTitle = value;
                Notify(nameof(EditTitle));
            }
        }
    }

    public string EditPlace
    {
        get => _editPlace;
        set
        {
            if (_editPlace == value)
            {
                return;
            }

            _editPlace = value;
            RebuildLocationOptions();
            Notify(nameof(EditPlace));
        }
    }

    public DateTime PlannedDate
    {
        get => _plannedDate;
        set
        {
            if (_plannedDate != value)
            {
                _plannedDate = value;
                Notify(nameof(PlannedDate));
            }
        }
    }

    public TimeSpan PlannedTime
    {
        get => _plannedTime;
        set
        {
            if (_plannedTime != value)
            {
                _plannedTime = value;
                Notify(nameof(PlannedTime));
            }
        }
    }

    public DateTimeOffset PlannedStartOn
    {
        get
        {
            var local = DateTime.SpecifyKind(_plannedDate.Date + _plannedTime, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, _clock.LocalTimeZone.GetUtcOffset(local));
        }
    }

    public void SelectLocation(string name)
    {
        EditPlace = TripConstants.TrimPlaceName(name) ?? string.Empty;
    }

    public async Task LoadAsync(Guid? tripId, CancellationToken cancellationToken)
    {
        try
        {
            _savedLocations = (await _preferences.GetCachedAsync(cancellationToken))?.Locations.Locations.Select(location => location.Name).ToArray() ?? [];
            if (tripId is { } id)
            {
                var first = _trip is null;
                _trip = await _trips.GetAsync(id, cancellationToken);
                _notFound = _trip is null;
                if (_trip is not null && first)
                {
                    Populate(_trip);
                }
            }

            RebuildLocationOptions();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _isLoading = false;
            Notify(string.Empty);
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanSave)
        {
            return false;
        }

        return await RunAsync("TripPlanSaveFailed", async () =>
        {
            _trip = _trip is null
                ? await _planned.PlanAsync(_editTitle, _editPlace, PlannedStartOn, cancellationToken)
                : await _planned.UpdatePlanAsync(_trip, _editTitle, _editPlace, PlannedStartOn, cancellationToken);
            Populate(_trip);
        });
    }

    public async Task<bool> CancelTripAsync(CancellationToken cancellationToken)
    {
        if (!CanCancelTrip || _trip is not { } trip)
        {
            return false;
        }

        var confirmed = await _interactions.ConfirmAsync(new DialogRequest(
            Text["TripPlanCancelConfirmTitle"], Text["TripPlanCancelConfirmMessage"], Text["TripPlanCancelConfirm"], Text["TripPlanKeep"], true), cancellationToken);
        if (!confirmed)
        {
            return false;
        }

        return await RunAsync("TripPlanSaveFailed", async () => _trip = await _planned.CancelPlanAsync(trip, cancellationToken));
    }

    public async Task RereadAfterSyncAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _trip is null || _isBusy || !_trip.HasPendingMetadata)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _rereadGeneration);
        var tripId = _trip.Detail.Trip.Id;
        var reread = await ReadPendingTripAsync(tripId, cancellationToken);
        await InvokeOnUiAsync(() => ApplyReread(generation, reread));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifier.SyncRunCompleted -= OnSyncRunCompleted;
        _disposeCancellation.Cancel();
    }

    private async Task<bool> RunAsync(string failureKey, Func<Task> action)
    {
        _isBusy = true;
        _messageKey = null;
        Notify(string.Empty);
        try
        {
            await action();
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _messageKey = failureKey;
            return false;
        }
        finally
        {
            _isBusy = false;
            Notify(string.Empty);
        }
    }

    private async void OnSyncRunCompleted(object? sender, EventArgs args)
    {
        try
        {
            await RereadAfterSyncAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The planned trip could not be reread after synchronisation.");
        }
    }

    private async Task<LocalTripRecordModel?> ReadPendingTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellation.Token);
            return await _trips.GetAsync(tripId, linked.Token);
        }
        catch (OperationCanceledException) when (_disposed)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The planned trip could not be reread after synchronisation.");
            return null;
        }
    }

    private void ApplyReread(int generation, LocalTripRecordModel? reread)
    {
        if (_disposed || reread is null || generation != Volatile.Read(ref _rereadGeneration))
        {
            return;
        }

        _trip = reread;
        if (!reread.HasPendingMetadata)
        {
            Populate(_trip);
        }

        Notify(string.Empty);
    }

    private Task InvokeOnUiAsync(Action action)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, null);
        return completion.Task;
    }

    private void Populate(LocalTripRecordModel trip)
    {
        _editTitle = trip.Detail.Trip.Title ?? string.Empty;
        _editPlace = trip.Detail.Trip.PlaceName ?? string.Empty;
        var start = TimeZoneInfo.ConvertTime(trip.Detail.Trip.PlannedStartOn ?? trip.Detail.Trip.StartedOn, _clock.LocalTimeZone).DateTime;
        _plannedDate = start.Date;
        _plannedTime = start.TimeOfDay;
    }

    private void SetDefaultStart()
    {
        var now = TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _clock.LocalTimeZone).DateTime;
        _plannedDate = now.Date;
        _plannedTime = new TimeSpan(now.Hour, 0, 0);
    }

    private void RebuildLocationOptions()
    {
        LocationOptions.Clear();
        foreach (var name in _savedLocations)
        {
            LocationOptions.Add(new CatchChipOptionModel(name, name, FishingLocationConstants.AreSameName(name, _editPlace)));
        }

        Notify(nameof(HasLocationOptions));
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
