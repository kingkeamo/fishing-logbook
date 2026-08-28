using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Features.Trips.Services;

public sealed class TripNoteWriteService : ITripNoteWriteService
{
    private readonly ITripNoteStore _noteStore;
    private readonly ITripClient _tripClient;

    public TripNoteWriteService(ITripNoteStore noteStore, ITripClient tripClient)
    {
        _noteStore = noteStore;
        _tripClient = tripClient;
    }

    public async Task<TripNoteModel> AddAsync(
        TripNoteDraftModel draft,
        TripNoteStorageEnum storage,
        CancellationToken cancellationToken)
    {
        var noteId = Guid.NewGuid();
        if (storage != TripNoteStorageEnum.Server)
        {
            var note = new TripNoteModel(
                noteId,
                draft.TripId,
                draft.OwnerUserId,
                draft.Text,
                draft.RecordedOn);
            await _noteStore.SaveAsync(note, cancellationToken);
            return note;
        }

        var recorded = await _tripClient.RecordNoteAsync(
            draft.TripId,
            new RecordTripNoteDto(noteId, draft.Text, draft.RecordedOn),
            cancellationToken);
        return new TripNoteModel(
            recorded?.Id ?? noteId,
            draft.TripId,
            draft.OwnerUserId,
            recorded?.Text ?? draft.Text,
            recorded?.RecordedOn ?? draft.RecordedOn,
            SyncStatus.Synchronised,
            DateTimeOffset.UtcNow);
    }

    public async Task RemoveAsync(
        TripNoteRemovalModel removal,
        TripNoteStorageEnum storage,
        CancellationToken cancellationToken)
    {
        if (storage == TripNoteStorageEnum.Server)
        {
            await _tripClient.DeleteNoteAsync(removal.TripId, removal.NoteId, cancellationToken);
            return;
        }

        if (removal.SyncStatus == SyncStatus.Synchronised)
        {
            await _tripClient.DeleteNoteAsync(removal.TripId, removal.NoteId, cancellationToken);
        }

        await _noteStore.DeleteAsync(
            removal.OwnerUserId,
            removal.TripId,
            removal.NoteId,
            cancellationToken);
    }
}
