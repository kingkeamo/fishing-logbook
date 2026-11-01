using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

/// <summary>Opens the trip modals and confirmations for the trip components. Every method returns true when something changed.</summary>
public interface ITripModalService
{
    /// <summary>Adds a note, or edits the note with the given id. Returns false without opening when that note no longer exists.</summary>
    Task<bool> EditNoteAsync(LocalTripRecordModel trip, Guid? noteId);

    Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId);

    Task<bool> AddCatchesAsync(LocalTripRecordModel trip);

    Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel);

    Task<bool> ShowParticipantsAsync(Guid tripId);
}
