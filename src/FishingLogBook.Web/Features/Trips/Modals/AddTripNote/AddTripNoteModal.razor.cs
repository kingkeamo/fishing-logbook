using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.AddTripNote;

public partial class AddTripNoteModal : ComponentBase, IDisposable
{
    private const int MaxNoteLength = TripConstants.MaxNoteTextLength;
    private const string LocalValueFormat = "yyyy-MM-ddTHH:mm";
    private const int LocalValueLength = 16;
    private const int DatePartLength = 10;
    private const int TimePartIndex = 11;
    private const int TimePartLength = 5;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private string _earliestLocal = string.Empty;
    private string _latestLocal = string.Empty;
    private string _dateLocal = string.Empty;
    private string _timeLocal = string.Empty;
    private string _text = string.Empty;
    private DateTimeOffset? _recordedOn;
    private string? _validationMessage;
    private string? _saveFailedMessage;
    private bool _isSaving;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public AddTripNoteModalModel Model { get; set; } = default!;

    [Inject]
    private ITripNoteWriteService NoteWriter { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string EarliestDate => DatePartOf(_earliestLocal);

    private string LatestDate => DatePartOf(_latestLocal);

    private DateTimeOffset Ceiling => Model.TripEndedOn ?? DateTimeOffset.UtcNow;

    private bool IsEditing => Model.ExistingNote is not null;

    private string Title => IsEditing ? Loc["Trip_NoteEdit"] : Loc["Trip_NoteAdd"];

    private string SaveLabel => IsEditing ? Loc["Modal_Save"] : Loc["Trip_NoteAdd"];

    private bool CanSave =>
        !_isSaving
        && TripConstants.IsNoteTextValid(_text)
        && _validationMessage is null
        && _recordedOn is not null;

    protected override async Task OnInitializedAsync()
    {
        _earliestLocal = await LocalValueAsync(Model.TripStartedOn);
        _latestLocal = await LocalValueAsync(Ceiling);
        var initial = await InitialLocalValueAsync();
        _dateLocal = DatePartOf(initial);
        _timeLocal = TimePartOf(initial);
        _text = Model.ExistingNote?.Text ?? string.Empty;
        await ValidateAsync();
    }

    private async Task<string> InitialLocalValueAsync()
    {
        if (Model.ExistingNote is { } existing)
        {
            return await LocalValueAsync(existing.RecordedOn);
        }

        if (Model.TripEndedOn is not null)
        {
            return _latestLocal;
        }

        var nowLocal = await LocalValueAsync(DateTimeOffset.UtcNow);
        if (_earliestLocal.Length < LocalValueLength || nowLocal.Length < LocalValueLength)
        {
            return _latestLocal.Length < LocalValueLength ? nowLocal : _latestLocal;
        }

        var candidate = string.Concat(
            _earliestLocal.AsSpan(0, TimePartIndex),
            nowLocal.AsSpan(TimePartIndex, TimePartLength));
        var instant = await ToInstantAsync(candidate);
        if (instant is null || instant < Model.TripStartedOn)
        {
            return _earliestLocal;
        }

        return instant > Ceiling ? _latestLocal : candidate;
    }

    private async Task OnDateChanged(string? value)
    {
        _dateLocal = value ?? string.Empty;
        _saveFailedMessage = null;
        await ValidateAsync();
    }

    private async Task OnTimeChanged(string? value)
    {
        _timeLocal = value ?? string.Empty;
        _saveFailedMessage = null;
        await ValidateAsync();
    }

    private void OnTextChanged(string? value)
    {
        _text = value ?? string.Empty;
        _saveFailedMessage = null;
    }

    private async Task ValidateAsync()
    {
        _validationMessage = null;
        _recordedOn = null;
        if (ComposedLocalValue() is not { } composed)
        {
            _validationMessage = Loc["Trip_NoteRecordedOnInvalid"].Value;
            return;
        }

        var instant = await ToInstantAsync(composed);
        if (instant is null)
        {
            _validationMessage = Loc["Trip_NoteRecordedOnInvalid"].Value;
            return;
        }

        if (instant < Model.TripStartedOn)
        {
            _validationMessage = Loc["Trip_NoteBeforeTripStart", Display(_earliestLocal)].Value;
            return;
        }

        if (instant > Ceiling)
        {
            _validationMessage = Model.TripEndedOn is null
                ? Loc["Trip_NoteAfterNow"].Value
                : Loc["Trip_NoteAfterTripEnd", Display(_latestLocal)].Value;
            return;
        }

        _recordedOn = instant;
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _saveFailedMessage = null;
        var text = TripConstants.TrimNoteText(_text);
        if (text is null || _recordedOn is not { } recordedOn)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var note = await PersistAsync(text, recordedOn);
            MudDialog.Close(DialogResult.Ok(new AddTripNoteModalResult(note)));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (HttpRequestException exception)
        {
            await FailAsync(exception, exception.StatusCode is null);
        }
        catch (TaskCanceledException exception)
        {
            await FailAsync(exception, unreachable: true);
        }
        catch (Exception exception)
        {
            await FailAsync(exception, unreachable: false);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<TripNoteModel> PersistAsync(string text, DateTimeOffset recordedOn)
    {
        if (Model.ExistingNote is { } existing)
        {
            return await NoteWriter.UpdateAsync(
                existing with { Text = text, RecordedOn = recordedOn },
                Model.Storage,
                _cancellationTokenSource.Token);
        }

        return await NoteWriter.AddAsync(
            new TripNoteDraftModel(Model.TripId, Model.OwnerUserId, text, recordedOn),
            Model.Storage,
            _cancellationTokenSource.Token);
    }

    private async Task FailAsync(Exception exception, bool unreachable)
    {
        await Logging.LogErrorAsync(Operation, exception, CancellationToken.None);
        _saveFailedMessage = unreachable && Model.Storage == TripStorageEnum.Server
            ? Loc["Trip_NoteOnlineRequired"].Value
            : Loc["Trip_NoteAddFailed"].Value;
    }

    private string? ComposedLocalValue()
    {
        if (_dateLocal.Length != DatePartLength || _timeLocal.Length < TimePartLength)
        {
            return null;
        }

        var value = $"{_dateLocal}T{_timeLocal[..TimePartLength]}";
        return Parse(value) is null ? null : value;
    }

    private async Task<DateTimeOffset?> ToInstantAsync(string localValue)
    {
        try
        {
            return await Time.FromDateTimeLocalValueAsync(
                localValue,
                _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading a trip note time",
                exception,
                CancellationToken.None);
            return null;
        }
    }

    private async Task<string> LocalValueAsync(DateTimeOffset instant)
    {
        try
        {
            return await Time.ToDateTimeLocalValueAsync(instant, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return string.Empty;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading a trip note time",
                exception,
                CancellationToken.None);
            return string.Empty;
        }
    }

    private static string DatePartOf(string localValue)
    {
        return localValue.Length >= LocalValueLength ? localValue[..DatePartLength] : string.Empty;
    }

    private static string TimePartOf(string localValue)
    {
        return localValue.Length >= LocalValueLength
            ? localValue.Substring(TimePartIndex, TimePartLength)
            : string.Empty;
    }

    private static string Display(string localValue)
    {
        return Parse(localValue) is { } parsed
            ? parsed.ToString("g", CultureInfo.CurrentCulture)
            : localValue;
    }

    private static DateTime? Parse(string localValue)
    {
        return DateTime.TryParseExact(
            localValue,
            LocalValueFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private string Operation => IsEditing ? "editing a trip note" : "adding a trip note";

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
