using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripNotes;

public partial class TripNotes : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<TripNoteModel> _notes = [];
    private readonly Dictionary<Guid, string> _localTimes = [];
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

    [Parameter]
    public TripNoteStorageEnum NoteStorage { get; set; } = TripNoteStorageEnum.LocalFirst;

    [Inject]
    private ITripNoteStore NoteStore { get; set; } = default!;

    [Inject]
    private ITripNoteWriteService NoteWriter { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

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
        if (NoteStorage == TripNoteStorageEnum.Server
            || Trip.OwnerUserId == Guid.Empty
            || Trip.Id == Guid.Empty)
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

    private async Task AddNoteAsync()
    {
        _removeFailed = false;
        var added = await ModalService
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                new AddTripNoteModalModel(
                    Trip.Id,
                    Trip.OwnerUserId,
                    Trip.StartedOn,
                    Trip.EndedOn,
                    NoteStorage),
                _cancellationTokenSource.Token);
        if (added is null)
        {
            return;
        }

        _notes.Add(added.Note);
        _notes.Sort(Chronologically);
        await RememberLocalTimeAsync(added.Note);
        await Changed.InvokeAsync();
    }

    private static int Chronologically(TripNoteModel left, TripNoteModel right)
    {
        var byRecordedOn = left.RecordedOn.CompareTo(right.RecordedOn);
        return byRecordedOn != 0 ? byRecordedOn : left.Id.CompareTo(right.Id);
    }

    public async Task RemoveNoteAsync(Guid noteId)
    {
        _removeFailed = false;
        var note = _notes.FirstOrDefault(candidate => candidate.Id == noteId);
        if (note is null && NoteStorage != TripNoteStorageEnum.Server)
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
            await NoteWriter.RemoveAsync(
                new TripNoteRemovalModel(
                    Trip.Id,
                    Trip.OwnerUserId,
                    noteId,
                    note?.SyncStatus ?? SyncStatus.Synchronised),
                NoteStorage,
                _cancellationTokenSource.Token);
            if (note is not null)
            {
                _notes.Remove(note);
            }

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
