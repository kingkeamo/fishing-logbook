using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>Add or edit one trip note, with the Web modal's date and time and the same trip-window validation.</summary>
public sealed class TripNoteEditorViewModel : INotifyPropertyChanged
{
    private readonly ITripService _trips;
    private readonly IAccountSessionCoordinator _session;
    private readonly TimeProvider _clock;
    private LocalTripRecordModel? _trip;
    private TripNoteDto? _existing;
    private DateTime? _local;
    private string _text = string.Empty;
    private bool _isSaving;
    private bool _saveFailed;

    public TripNoteEditorViewModel(ITripService trips, IAccountSessionCoordinator session, UiTextService text, TimeProvider clock)
    {
        _trips = trips;
        _session = session;
        _clock = clock;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public bool IsEditing => _existing is not null;

    public string Heading => Text[IsEditing ? "TripNoteEdit" : "TripNoteAdd"];

    public string SaveText => Text[IsEditing ? "Save" : "TripNoteAdd"];

    public string NoteText
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            _saveFailed = false;
            Notify();
        }
    }

    public DateTime NoteDate
    {
        get => (_local ?? DefaultLocal()).Date;
        set => SetLocal(value.Date + NoteTime);
    }

    public TimeSpan NoteTime
    {
        get => TimeSpan.FromMinutes((int)(_local ?? DefaultLocal()).TimeOfDay.TotalMinutes);
        set => SetLocal(NoteDate + TimeSpan.FromMinutes((int)value.TotalMinutes));
    }

    public DateTime NoteDateMinimum => _trip is null ? DateTime.MinValue : Local(_trip.Detail.Trip.StartedOn).Date;

    public DateTime NoteDateMaximum => _trip is null ? DateTime.MaxValue : Local(Ceiling()).Date;

    public bool HasWhenError => WhenKey() is not null;

    public string WhenText => WhenKey() is { } key
        ? string.Format(Text.Culture, Text[key], key == "TripNoteBeforeTripStart" ? Format(Local(_trip!.Detail.Trip.StartedOn)) : Format(Local(Ceiling())))
        : string.Empty;

    public bool CanSave => !_isSaving && TripConstants.IsNoteTextValid(_text) && !HasWhenError;

    public bool IsSaving => _isSaving;

    public bool SaveFailed => _saveFailed;

    public string FailureText => Text["TripNoteAddFailed"];

    public void Initialize(LocalTripRecordModel trip, TripNoteDto? existing)
    {
        _trip = trip;
        _existing = existing;
        _text = existing?.Text ?? string.Empty;
        _local = existing is null ? null : Local(existing.RecordedOn);
        _isSaving = false;
        _saveFailed = false;
        Notify();
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanSave || _trip is not { } trip || _session.Capture() is not { } lease)
        {
            return false;
        }

        _isSaving = true;
        _saveFailed = false;
        Notify();
        try
        {
            var note = new TripNoteDto(_existing?.Id ?? Guid.NewGuid(), trip.Detail.Trip.Id, TripConstants.TrimNoteText(_text)!, ResolveInstant())
            {
                CreatedByUserId = lease.UserId
            };
            await _trips.SaveNoteAsync(trip, note, deleted: false, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _saveFailed = true;
            return false;
        }
        finally
        {
            _isSaving = false;
            Notify();
        }
    }

    private void SetLocal(DateTime value)
    {
        _local = value;
        _saveFailed = false;
        Notify();
    }

    private DateTimeOffset Ceiling()
    {
        var now = _clock.GetUtcNow();
        return _trip?.Detail.Trip.EndedOn is { } ended && ended < now ? ended : now;
    }

    private DateTime DefaultLocal()
    {
        if (_trip is null)
        {
            return Local(_clock.GetUtcNow());
        }

        var ceiling = Ceiling();
        var started = _trip.Detail.Trip.StartedOn;
        return Local(ceiling < started ? started : ceiling);
    }

    private DateTimeOffset ResolveInstant()
    {
        if (_local is not { } local)
        {
            return Ceiling();
        }

        if (_existing is { } existing && Local(existing.RecordedOn).ToString("yyyyMMddHHmm") == local.ToString("yyyyMMddHHmm"))
        {
            return existing.RecordedOn;
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, _clock.LocalTimeZone), TimeSpan.Zero);
    }

    private string? WhenKey()
    {
        if (_trip is null || _local is null)
        {
            return null;
        }

        var instant = ResolveInstant();
        if (instant < _trip.Detail.Trip.StartedOn)
        {
            return "TripNoteBeforeTripStart";
        }

        return instant > Ceiling()
            ? _trip.Detail.Trip.EndedOn is null ? "TripNoteAfterNow" : "TripNoteAfterTripEnd"
            : null;
    }

    private DateTime Local(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, _clock.LocalTimeZone).DateTime;
    }

    private string Format(DateTime local)
    {
        return local.ToString("g", Text.Culture);
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
