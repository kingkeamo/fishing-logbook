using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
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
/// The Web trip page: header with status, facts and counts, the chronological timeline of catches, photographs
/// and notes, and the contributor actions. Notes, adding catches and participants are popups, and updating the
/// trip is its own screen, exactly as on Web. Writes go to SQLite first and synchronise later.
/// </summary>
public sealed class TripDetailViewModel : INotifyPropertyChanged
{
    private readonly ITripService _trips;
    private readonly ITripTimelineService _timeline;
    private readonly ICatchStore _catchStore;
    private readonly IPhotographFileStore _files;
    private readonly ITripPhotographService _photographs;
    private readonly IAccountSessionCoordinator _session;
    private readonly IAccountUnitsService _units;
    private readonly IMeasurementService _measurement;
    private readonly TimeProvider _clock;
    private LocalTripRecordModel? _trip;
    private MeasurementUnitsModel _unitsModel = MeasurementUnitsModel.Default;
    private IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();
    private Guid _viewerId;
    private bool _isLoading = true;
    private bool _notFound;
    private bool _isBusy;
    private bool _showPhotographPicker;
    private string? _messageKey;

    public TripDetailViewModel(
        ITripService trips,
        ITripTimelineService timeline,
        ICatchStore catchStore,
        IPhotographFileStore files,
        ITripPhotographService photographs,
        IAccountSessionCoordinator session,
        IAccountUnitsService units,
        IMeasurementService measurement,
        UiTextService text,
        TimeProvider clock)
    {
        _trips = trips;
        _timeline = timeline;
        _catchStore = catchStore;
        _files = files;
        _photographs = photographs;
        _session = session;
        _units = units;
        _measurement = measurement;
        _clock = clock;
        Text = text;
        text.PropertyChanged += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<TripTimelineRowModel> Timeline { get; } = [];

    public LocalTripRecordModel? Trip => _trip;

    public bool IsPlannedLifecycle => _trip is { HasPlannedLifecycle: true };

    public Guid? TripId => _trip?.Detail.Trip.Id;

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value, nameof(IsLoading), nameof(ShowContent), nameof(CanInteract));
    }

    public bool NotFound => _notFound;

    public bool ShowContent => !IsLoading && _trip is not null;

    public bool CanInteract => !IsLoading && !_isBusy;

    public bool IsCompleted => _trip?.Detail.Trip.Status == TripConstants.Completed;

    public bool CanManageTrip => _trip is { CanManage: true } && !IsCompleted;

    public bool CanAddNotes => _trip is { CanContribute: true };

    public bool CanAddCatches => _trip is { CanContribute: true };

    public bool CanRecordCatch => _trip is { CanContribute: true } && !IsCompleted;

    public bool CanAddPhotographs => CanRecordCatch;

    public bool ShowBackToLogbook => !CanManageTrip;

    public string DateHeading => _trip is null ? string.Empty : Local(_trip.Detail.Trip.StartedOn).ToString("d MMM yyyy", Text.Culture);

    public string TitleText => _trip?.Detail.Trip.Title ?? string.Empty;

    public bool HasTitle => !string.IsNullOrWhiteSpace(TitleText);

    public string PlaceText => _trip?.Detail.Trip.PlaceName ?? string.Empty;

    public bool HasPlace => !string.IsNullOrWhiteSpace(PlaceText);

    public string StartedText => _trip is null ? string.Empty : Local(_trip.Detail.Trip.StartedOn).ToString("t", Text.Culture);

    public string ElapsedText => _trip is null ? string.Empty : FormatDuration((_trip.Detail.Trip.EndedOn ?? _clock.GetUtcNow()) - _trip.Detail.Trip.StartedOn);

    public string StatusText => Text[IsCompleted ? "TripStatusFinished" : "TripActiveLabel"];

    public string StatusIcon => IsCompleted ? "icon_check.png" : "icon_play.png";

    public string CatchSummary => Count(Timeline.Count(row => row.IsCatch), "TripCatchesNone", "TripCatchesOne", "TripCatchesMany");

    public string PhotographSummary => Count(Timeline.Count(row => row.IsPhotograph), "TripListPhotographsNone", "TripListPhotographsOne", "TripListPhotographsMany");

    public string NoteSummary => Count(Timeline.Count(row => row.IsNote), "TripListNotesNone", "TripListNotesOne", "TripListNotesMany");

    public bool ShowPhotographPicker
    {
        get => _showPhotographPicker;
        private set => Set(ref _showPhotographPicker, value, nameof(ShowPhotographPicker), nameof(Active));
    }

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public ActiveTripModel? Active => _trip is null
        ? null
        : new ActiveTripModel(_trip, Text, DateHeading, TitleText, PlaceText, StartedText, ElapsedText, StatusText, StatusIcon, IsCompleted,
            CatchSummary, PhotographSummary, NoteSummary, Timeline.ToArray(), CanRecordCatch, CanAddCatches, CanAddPhotographs, CanManageTrip,
            ShowBackToLogbook, ShowPhotographPicker, MessageText);

    public async Task LoadAsync(Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            _viewerId = _session.Capture()?.UserId ?? Guid.Empty;
            _unitsModel = await _units.GetAsync(cancellationToken);
            var trip = await _trips.GetAsync(tripId, cancellationToken);
            _notFound = trip is null;
            _trip = trip;
            if (trip is not null)
            {
                var local = await _catchStore.ListAsync(cancellationToken);
                _names = trip.Detail.Contributors.ToDictionary(
                    contributor => contributor.UserId,
                    contributor => string.IsNullOrWhiteSpace(contributor.DisplayName) ? Text["TripContributorUnknown"] : contributor.DisplayName);
                BuildTimeline(_timeline.Build(trip.Detail, local));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsLoading = false;
            Refresh();
        }
    }

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        return _trip is { } trip ? LoadAsync(trip.Detail.Trip.Id, cancellationToken) : Task.CompletedTask;
    }

    public void TogglePhotographPicker()
    {
        if (CanAddPhotographs)
        {
            ShowPhotographPicker = !ShowPhotographPicker;
        }
    }

    public async Task AddPhotographAsync(bool useCamera, CancellationToken cancellationToken)
    {
        if (!CanAddPhotographs || _isBusy || _trip is not { } trip)
        {
            return;
        }

        _isBusy = true;
        _messageKey = null;
        Refresh();
        try
        {
            _messageKey = await _photographs.AddAsync(trip, useCamera, cancellationToken);
            if (_messageKey is null)
            {
                ShowPhotographPicker = false;
                await LoadAsync(trip.Detail.Trip.Id, cancellationToken);
            }
        }
        finally
        {
            _isBusy = false;
            Refresh();
        }
    }

    public async Task FinishAsync(CancellationToken cancellationToken)
    {
        if (!CanManageTrip || _trip is not { } trip)
        {
            return;
        }

        await RunAsync("TripEditSaveFailed", async () =>
        {
            var ended = _clock.GetUtcNow();
            await _trips.UpdateAsync(trip, trip.Detail.Trip with { Status = TripConstants.Completed, EndedOn = ended }, cancellationToken);
        }, cancellationToken);
    }

    private async Task RunAsync(string failureKey, Func<Task> action, CancellationToken cancellationToken)
    {
        if (_isBusy || _trip is not { } trip)
        {
            return;
        }

        _isBusy = true;
        _messageKey = null;
        Refresh();
        try
        {
            await action();
            await LoadAsync(trip.Detail.Trip.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _messageKey = failureKey;
        }
        finally
        {
            _isBusy = false;
            Refresh();
        }
    }

    private void BuildTimeline(IReadOnlyList<TripTimelineItemModel> items)
    {
        Timeline.Clear();
        var distinctDays = items.Select(item => Local(item.OccurredOn).Date).Distinct().Count();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var showDate = index == 0
                ? distinctDays > 1
                : Local(items[index - 1].OccurredOn).Date != Local(item.OccurredOn).Date;
            Timeline.Add(ToRow(item, showDate, index == items.Count - 1));
        }
    }

    private bool CanEditCatch(TripTimelineItemModel item)
    {
        return item.ContributedByUserId == _viewerId && (item.RecordedByUserId == Guid.Empty || item.RecordedByUserId == _viewerId);
    }

    private TripTimelineRowModel ToRow(TripTimelineItemModel item, bool showDate, bool isLast)
    {
        var local = Local(item.OccurredOn);
        var time = showDate
            ? $"{local.ToString("d MMM", Text.Culture)} · {local.ToString("t", Text.Culture)}"
            : local.ToString("t", Text.Culture);
        var others = item.ContributedByUserId != Guid.Empty && item.ContributedByUserId != _viewerId;
        var contributor = item.Kind == TripTimelineKindEnum.Catch || others ? NameOf(item.ContributedByUserId) : null;
        var pending = item.IsPending ? Text["CatchSyncWaiting"] : null;
        var row = item.Kind switch
        {
            TripTimelineKindEnum.Started => new TripTimelineRowModel(item.Kind, time, null, Text["TripTimelineStarted"], Text["TripTimelineStartedDescription"], null, null, null, false, null),
            TripTimelineKindEnum.Finished => new TripTimelineRowModel(item.Kind, time, null, Text["TripTimelineFinished"], Text["TripTimelineFinishedDescription"], null, null, null, false, null),
            TripTimelineKindEnum.Note => new TripTimelineRowModel(item.Kind, time, contributor, Text["TripTimelineNote"], item.Text, null, null, item.NoteId,
                item.ContributedByUserId == _viewerId && CanAddNotes, null),
            TripTimelineKindEnum.Photograph => new TripTimelineRowModel(item.Kind, time, contributor, Text["TripTimelinePhotograph"],
                Text["TripTimelinePhotographDescription"], item.PhotographUrl, null, null, false, null),
            _ => new TripTimelineRowModel(item.Kind, time, contributor,
                string.IsNullOrWhiteSpace(item.SpeciesName) ? Text["TripTimelineCatchUnknownSpecies"] : item.SpeciesName,
                MeasurementText(item.Weight, item.Length), CatchPhoto(item), item.CatchId, null, false, pending)
            {
                RecordedByText = RecordedBy(item),
                CanEditCatch = CanEditCatch(item)
            }
        };
        return row with { IsLast = isLast };
    }

    private string NameOf(Guid userId)
    {
        return _names.TryGetValue(userId, out var name) ? name : Text["TripContributorUnknown"];
    }

    private string? RecordedBy(TripTimelineItemModel item)
    {
        return item.RecordedByUserId != Guid.Empty && item.RecordedByUserId != item.ContributedByUserId
            ? string.Format(Text.Culture, Text["TripTimelineRecordedBy"], NameOf(item.RecordedByUserId))
            : null;
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

    private string? CatchPhoto(TripTimelineItemModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.LocalFilePath) && _files.TryResolveRelativePath(_viewerId, item.LocalFilePath, out var absolutePath))
        {
            return absolutePath;
        }

        return string.IsNullOrWhiteSpace(item.PhotographUrl) ? null : item.PhotographUrl;
    }

    private DateTime Local(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, _clock.LocalTimeZone).DateTime;
    }

    private string FormatDuration(TimeSpan elapsed)
    {
        var safe = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        return (int)safe.TotalHours > 0
            ? string.Format(Text.Culture, Text["TripDurationHoursMinutes"], (int)safe.TotalHours, safe.Minutes)
            : string.Format(Text.Culture, Text["TripDurationMinutes"], safe.Minutes);
    }

    private string Count(int count, string none, string one, string many)
    {
        return count switch
        {
            0 => Text[none],
            1 => Text[one],
            _ => string.Format(Text.Culture, Text[many], count)
        };
    }

    private void Refresh()
    {
        Notify(string.Empty);
    }

    private void Set<T>(ref T field, T value, params string[] names)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Notify(names);
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
