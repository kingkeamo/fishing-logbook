using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.AddTripNote;

public partial class AddTripNoteModal : ComponentBase, IDisposable
{
    private const int MaxNoteLength = TripConstants.MaxNoteTextLength;
    private const int LocalValueLength = 16;
    private const int TimePartIndex = 11;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private string _recordedOnLocal = string.Empty;
    private string _text = string.Empty;
    private bool _isSaving;
    private bool _recordedOnInvalid;
    private bool _saveFailed;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public AddTripNoteModalModel Model { get; set; } = default!;

    [Inject]
    private ITripNoteStore NoteStore { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanSave => !_isSaving && TripConstants.IsNoteTextValid(_text);

    protected override async Task OnInitializedAsync()
    {
        _recordedOnLocal = await ResolveDefaultRecordedOnAsync();
    }

    private async Task<string> ResolveDefaultRecordedOnAsync()
    {
        try
        {
            var tripLocal = await Time.ToDateTimeLocalValueAsync(
                Model.TripStartedOn,
                _cancellationTokenSource.Token);
            var nowLocal = await Time.ToDateTimeLocalValueAsync(
                DateTimeOffset.UtcNow,
                _cancellationTokenSource.Token);
            return OnTripDate(tripLocal, nowLocal);
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

    private static string OnTripDate(string tripLocal, string nowLocal)
    {
        if (tripLocal.Length < LocalValueLength || nowLocal.Length < LocalValueLength)
        {
            return nowLocal;
        }

        var candidate = string.Concat(
            tripLocal.AsSpan(0, TimePartIndex),
            nowLocal.AsSpan(TimePartIndex, LocalValueLength - TimePartIndex));
        return string.CompareOrdinal(candidate, tripLocal) < 0 ? tripLocal : candidate;
    }

    private void OnRecordedOnChanged(string? value)
    {
        _recordedOnLocal = value ?? string.Empty;
        _recordedOnInvalid = false;
        _saveFailed = false;
    }

    private void OnTextChanged(string? value)
    {
        _text = value ?? string.Empty;
        _saveFailed = false;
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _recordedOnInvalid = false;
        _saveFailed = false;
        var text = TripConstants.TrimNoteText(_text);
        if (text is null)
        {
            return;
        }

        var recordedOn = await ResolveRecordedOnAsync();
        if (recordedOn is null)
        {
            _recordedOnInvalid = true;
            return;
        }

        var note = new TripNoteModel(
            Guid.NewGuid(),
            Model.TripId,
            Model.OwnerUserId,
            text,
            recordedOn.Value);
        _isSaving = true;
        try
        {
            await NoteStore.SaveAsync(note, _cancellationTokenSource.Token);
            MudDialog.Close(DialogResult.Ok(new AddTripNoteModalResult(note)));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("adding a trip note", exception, CancellationToken.None);
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<DateTimeOffset?> ResolveRecordedOnAsync()
    {
        if (string.IsNullOrWhiteSpace(_recordedOnLocal))
        {
            return null;
        }

        try
        {
            return await Time.FromDateTimeLocalValueAsync(
                _recordedOnLocal,
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
