using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

/// <summary>Opens the trip modals and confirmations for the trip components. Every method returns true when something changed.</summary>
public interface ITripModalService
{
    Task<bool> EditNoteAsync(LocalTripRecordModel trip, TripNoteDto? existing);

    Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId);

    Task<bool> AddCatchesAsync(LocalTripRecordModel trip);

    Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel);

    Task<bool> ShowParticipantsAsync(Guid tripId);
}
