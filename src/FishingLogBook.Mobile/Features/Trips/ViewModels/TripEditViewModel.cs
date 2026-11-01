using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>
/// The Web "Update trip" screen: title and location are saved with Save; photographs, notes and catches change as
/// soon as they are edited, exactly as on Web. Everything is written to SQLite first and synchronised later.
/// </summary>
public sealed class TripEditViewModel : INotifyPropertyChanged
{
    private readonly ITripService _trips;
    private readonly ITripTimelineService _timeline;
    private readonly ITripPhotographService _photographs;
    private readonly ICatchStore _catchStore;
    private readonly IPhotographFileStore _files;
    private readonly IAnglerPreferencesService _preferences;
    private readonly IAccountSessionCoordinator _session;
    private readonly IAccountUnitsService _units;
    private readonly IMeasurementService _measurement;
    private readonly TimeProvider _clock;
    private LocalTripRecordModel? _trip;
    private MeasurementUnitsModel _unitsModel = MeasurementUnitsModel.Default;
    private IReadOnlyList<string> _savedLocations = [];
    private Guid _viewerId;
    private bool _isLoading = true;
    private bool _notFound;
    private bool _isBusy;
    private string _editTitle = string.Empty;
    private string _editPlace = string.Empty;
    private string? _messageKey;

    public TripEditViewModel(
        ITripService trips,
        ITripTimelineService timeline,
        ITripPhotographService photographs,
        ICatchStore catchStore,
        IPhotographFileStore files,
        IAnglerPreferencesService preferences,
        IAccountSessionCoordinator session,
        IAccountUnitsService units,
        IMeasurementService measurement,
        UiTextService text,
        TimeProvider clock)
    {
        _trips = trips;
        _timeline = timeline;
        _photographs = photographs;
        _catchStore = catchStore;
        _files = files;
        _preferences = preferences;
        _session = session;
        _units = units;
        _measurement = measurement;
        _clock = clock;
        Text = text;
        text.PropertyChanged += (_, _) => Notify(string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<CatchChipOptionModel> LocationOptions { get; } = [];

    public ObservableCollection<TripNoteRowModel> Notes { get; } = [];

    public ObservableCollection<TripPhotographRowModel> Photographs { get; } = [];

    public ObservableCollection<TripEditCatchModel> Catches { get; } = [];

    public LocalTripRecordModel? Trip => _trip;

    public Guid? TripId => _trip?.Detail.Trip.Id;

    public bool IsLoading => _isLoading;

    public bool NotFound => _notFound;

    public bool ShowContent => !_isLoading && _trip is not null;

    public bool CanInteract => !_isLoading && !_isBusy;

    public bool CanManageTrip => _trip is { CanManage: true } && _trip.Detail.Trip.Status == TripConstants.Active;

    public bool CanAddNotes => _trip is { CanContribute: true };

    public string SummaryText => _trip is null
        ? string.Empty
        : $"{Local(_trip.Detail.Trip.StartedOn).ToString("d MMM yyyy", Text.Culture)} · {Local(_trip.Detail.Trip.StartedOn).ToString("t", Text.Culture)}";

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
            Notify(nameof(EditPlace), nameof(HasEditPlace));
        }
    }

    public bool HasEditPlace => !string.IsNullOrWhiteSpace(_editPlace);

    public bool HasLocationOptions => LocationOptions.Count > 0;

    public bool NoCatches => Catches.Count == 0;

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public async Task LoadAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var first = _trip is null || _trip.Detail.Trip.Id != tripId;
        try
        {
            _viewerId = _session.Capture()?.UserId ?? Guid.Empty;
            _unitsModel = await _units.GetAsync(cancellationToken);
            _savedLocations = (await _preferences.GetCachedAsync(cancellationToken))?.Locations.Locations.Select(location => location.Name).ToArray() ?? [];
            var trip = await _trips.GetAsync(tripId, cancellationToken);
            _notFound = trip is null;
            _trip = trip;
            if (trip is null)
            {
                return;
            }

            if (first)
            {
                _editTitle = trip.Detail.Trip.Title ?? string.Empty;
                _editPlace = trip.Detail.Trip.PlaceName ?? string.Empty;
            }

            var local = await _catchStore.ListAsync(cancellationToken);
            Build(trip, _timeline.Build(trip.Detail, local));
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

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        return _trip is { } trip ? LoadAsync(trip.Detail.Trip.Id, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>Saves the title and location. Returns true when the screen can close.</summary>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManageTrip || _trip is not { } trip || _isBusy)
        {
            return false;
        }

        return await RunAsync("TripEditSaveFailed", async () =>
            await _trips.UpdateAsync(trip, trip.Detail.Trip with { Title = _editTitle, PlaceName = _editPlace }, cancellationToken), cancellationToken, reload: false);
    }

    public async Task AddPhotographAsync(bool useCamera, CancellationToken cancellationToken)
    {
        if (!CanManageTrip || _isBusy || _trip is not { } trip)
        {
            return;
        }

        _isBusy = true;
        _messageKey = null;
        Notify(string.Empty);
        try
        {
            _messageKey = await _photographs.AddAsync(trip, useCamera, cancellationToken);
        }
        finally
        {
            _isBusy = false;
        }

        await LoadAsync(trip.Detail.Trip.Id, cancellationToken);
    }

    public async Task RemovePhotographAsync(Guid photographId, CancellationToken cancellationToken)
    {
        if (_trip is not { } trip || _isBusy || Photographs.All(photograph => photograph.Id != photographId))
        {
            return;
        }

        var removed = false;
        await RunAsync("TripPhotographRemoveFailed", async () => removed = await _photographs.RemoveAsync(trip, photographId, cancellationToken), cancellationToken);
        if (!removed && _messageKey is null)
        {
            _messageKey = "TripPhotographRemoveFailed";
            Notify(nameof(HasMessage), nameof(MessageText));
        }
    }

    private async Task<bool> RunAsync(string failureKey, Func<Task> action, CancellationToken cancellationToken, bool reload = true)
    {
        var trip = _trip!;
        _isBusy = true;
        _messageKey = null;
        Notify(string.Empty);
        try
        {
            await action();
            if (reload)
            {
                await LoadAsync(trip.Detail.Trip.Id, cancellationToken);
            }

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

    private void Build(LocalTripRecordModel trip, IReadOnlyList<TripTimelineItemModel> items)
    {
        Notes.Clear();
        Photographs.Clear();
        Catches.Clear();
        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case TripTimelineKindEnum.Note:
                    Notes.Add(new TripNoteRowModel(item.NoteId ?? Guid.Empty, Local(item.OccurredOn).ToString("g", Text.Culture), item.Text ?? string.Empty,
                        item.ContributedByUserId == _viewerId && CanAddNotes));
                    break;
                case TripTimelineKindEnum.Photograph:
                    var own = item.ContributedByUserId == _viewerId;
                    Photographs.Add(new TripPhotographRowModel(item.PhotographId ?? Guid.Empty, item.PhotographUrl, own, Text["TripPhotographRemove"]));
                    break;
                case TripTimelineKindEnum.Catch when item.CatchId is { } catchId
                    && item.ContributedByUserId == _viewerId && (item.RecordedByUserId == Guid.Empty || item.RecordedByUserId == _viewerId):
                    var species = string.IsNullOrWhiteSpace(item.SpeciesName) ? Text["TripTimelineCatchUnknownSpecies"] : item.SpeciesName;
                    Catches.Add(new TripEditCatchModel(catchId, species, MeasurementText(item.Weight, item.Length), string.Format(Text.Culture, Text["TripCatchRemoveFromTripFor"], species)));
                    break;
            }
        }
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

    private string? MeasurementText(decimal? weightKg, decimal? lengthCm)
    {
        var parts = new List<string>();
        if (_measurement.ToDisplayWeight(weightKg, _unitsModel.WeightUnit) is { } weight)
        {
            parts.Add($"{weight.ToString("0.##", Text.Culture)} {Text[_unitsModel.WeightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"]}");
        }

        if (_measurement.ToDisplayLength(lengthCm, _unitsModel.LengthUnit) is { } length)
        {
            parts.Add($"{length.ToString("0.##", Text.Culture)} {Text[_unitsModel.LengthUnit == LengthUnitEnum.In ? "MeasureUnitIn" : "MeasureUnitCm"]}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private DateTime Local(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, _clock.LocalTimeZone).DateTime;
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
