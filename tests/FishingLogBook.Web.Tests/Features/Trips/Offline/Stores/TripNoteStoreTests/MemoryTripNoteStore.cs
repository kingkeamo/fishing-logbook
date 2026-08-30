using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;

public sealed class MemoryTripNoteStore : ITripNoteStore
{
    private readonly Dictionary<Guid, TripNoteModel> _notes = [];

    public bool FailWrite { get; set; }

    public int PendingCalls { get; private set; }

    public int PendingTripCalls { get; private set; }

    public Func<Guid, Task>? BeforePendingRead { get; set; }

    public Task SaveAsync(TripNoteModel note, CancellationToken cancellationToken)
    {
        if (note.CreatedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip note requires an owner.");
        }

        if (string.IsNullOrWhiteSpace(note.Text))
        {
            throw new InvalidOperationException("A trip note requires text.");
        }

        if (FailWrite)
        {
            throw new InvalidOperationException("Trip note persistence failed.");
        }

        _notes[note.Id] = note;
        return Task.CompletedTask;
    }

    public int DeleteCalls { get; private set; }

    public Task<bool> DeleteAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        DeleteCalls++;
        if (!_notes.TryGetValue(noteId, out var note)
            || note.CreatedByUserId != ownerUserId
            || note.TripId != tripId)
        {
            return Task.FromResult(false);
        }

        _notes.Remove(noteId);
        return Task.FromResult(true);
    }

    public int ForTripCalls { get; private set; }

    public Task<IReadOnlyList<TripNoteModel>> GetForTripAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        ForTripCalls++;
        return Task.FromResult<IReadOnlyList<TripNoteModel>>(
            [.. _notes.Values
                .Where(note => note.CreatedByUserId == ownerUserId && note.TripId == tripId)
                .OrderBy(note => note.RecordedOn)
                .ThenBy(note => note.Id)]);
    }

    public async Task<TripNoteModel?> GetAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var notes = await GetForTripAsync(ownerUserId, tripId, cancellationToken);
        return notes.FirstOrDefault(note => note.Id == noteId);
    }

    public async Task<IReadOnlyList<TripNoteModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        PendingCalls++;
        if (BeforePendingRead is not null)
        {
            await BeforePendingRead(ownerUserId);
        }

        return
        [
            .. _notes.Values
                .Where(note =>
                    note.CreatedByUserId == ownerUserId
                    && note.SyncStatus != SyncStatus.Synchronised)
                .OrderBy(note => note.RecordedOn)
                .ThenBy(note => note.Id)
        ];
    }

    public Task<IReadOnlyCollection<Guid>> GetTripsWithPendingNotesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        PendingTripCalls++;
        return Task.FromResult<IReadOnlyCollection<Guid>>(
            [.. _notes.Values
                .Where(note =>
                    note.CreatedByUserId == ownerUserId
                    && note.SyncStatus != SyncStatus.Synchronised
                    && note.SyncStatus != SyncStatus.FailedToSynchronise)
                .Select(note => note.TripId)
                .Distinct()]);
    }

    public TripNoteModel? Stored(Guid noteId)
    {
        return _notes.TryGetValue(noteId, out var note) ? note : null;
    }

    public int Count => _notes.Count;

    public IReadOnlyList<TripNoteModel> All()
    {
        return [.. _notes.Values.OrderBy(note => note.RecordedOn).ThenBy(note => note.Id)];
    }
}
