using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripNotes;

public partial class TripNotes : ComponentBase, IDisposable
{
    private const int MaxNoteLength = TripConstants.MaxNoteTextLength;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<TripNoteModel> _notes = [];
    private readonly Dictionary<Guid, string> _localTimes = [];
    private string _text = string.Empty;
    private bool _isWriting;
    private bool _addFailed;
    private bool _removeFailed;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public EventCallback Changed { get; set; }

    [Parameter]
    public bool ShowList { get; set; } = true;

    [Parameter]
    public bool UseFloatingTrigger { get; set; }

    [Inject]
    private ITripNoteStore NoteStore { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanSave => TripConstants.IsNoteTextValid(_text);

    protected override async Task OnInitializedAsync()
    {
        await LoadStoredNotesAsync();
    }

    private async Task LoadStoredNotesAsync()
    {
        var stored = await ReadStoredNotesAsync();
        _notes.Clear();
        _notes.AddRange(stored.OrderBy(note => note.RecordedOn).ThenBy(note => note.Id));
        foreach (var note in _notes)
        {
            await RememberLocalTimeAsync(note);
        }
    }

    private async Task<IReadOnlyList<TripNoteModel>> ReadStoredNotesAsync()
    {
        if (Trip.OwnerUserId == Guid.Empty || Trip.Id == Guid.Empty)
        {
            return Trip.Notes ?? [];
        }

        try
        {
            return await NoteStore.GetForTripAsync(
                Trip.OwnerUserId,
                Trip.Id,
                _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return [];
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading trip notes", exception, CancellationToken.None);
            return Trip.Notes ?? [];
        }
    }

    private string LocalTime(Guid noteId)
    {
        return _localTimes.TryGetValue(noteId, out var value) ? value : string.Empty;
    }

    private void StartWriting()
    {
        _isWriting = true;
        _addFailed = false;
        _removeFailed = false;
    }

    private void CancelWriting()
    {
        _isWriting = false;
        _text = string.Empty;
        _addFailed = false;
    }

    private void OnTextChanged(string? value)
    {
        _text = value ?? string.Empty;
    }

    private async Task AddNoteAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _addFailed = false;
        var text = TripConstants.TrimNoteText(_text);
        if (text is null)
        {
            return;
        }

        var note = new TripNoteModel(
            Guid.NewGuid(),
            Trip.Id,
            Trip.OwnerUserId,
            text,
            DateTimeOffset.UtcNow);
        try
        {
            await NoteStore.SaveAsync(note, _cancellationTokenSource.Token);
            _notes.Add(note);
            await RememberLocalTimeAsync(note);
            _text = string.Empty;
            _isWriting = false;
            await Changed.InvokeAsync();
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("adding a trip note", exception, CancellationToken.None);
            _addFailed = true;
        }
    }

    public async Task RemoveNoteAsync(Guid noteId)
    {
        _removeFailed = false;
        var note = _notes.FirstOrDefault(candidate => candidate.Id == noteId);
        if (note is null)
        {
            return;
        }

        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Trip_NoteRemoveTitle"].Value,
                Loc["Trip_NoteRemoveMessage"].Value,
                Loc["Trip_NoteRemoveConfirm"].Value,
                Loc["Modal_Cancel"].Value,
                IsDestructive: true),
            _cancellationTokenSource.Token);
        if (!confirmed)
        {
            return;
        }

        try
        {
            if (note.SyncStatus == SyncStatus.Synchronised)
            {
                await TripClient.DeleteNoteAsync(Trip.Id, noteId, _cancellationTokenSource.Token);
            }

            await NoteStore.DeleteAsync(
                Trip.OwnerUserId,
                Trip.Id,
                noteId,
                _cancellationTokenSource.Token);
            _notes.Remove(note);
            _localTimes.Remove(noteId);
            await Changed.InvokeAsync();
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("removing a trip note", exception, CancellationToken.None);
            _removeFailed = true;
        }
    }

    private async Task RememberLocalTimeAsync(TripNoteModel note)
    {
        try
        {
            var value = await Time.ToDateTimeLocalValueAsync(
                note.RecordedOn,
                _cancellationTokenSource.Token);
            _localTimes[note.Id] = value.Length >= 16 ? value[11..16] : value;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a trip note time", exception, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
