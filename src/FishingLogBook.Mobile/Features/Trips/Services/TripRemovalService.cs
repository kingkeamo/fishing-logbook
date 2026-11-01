using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripRemovalService(ITripService trips, ITripCatchService tripCatches) : ITripRemovalService
{
    public async Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId, CancellationToken cancellationToken)
    {
        if (trip.FindNote(noteId) is not { } note)
        {
            return false;
        }

        try
        {
            await trips.SaveNoteAsync(trip, note, deleted: true, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    public Task<bool> RemoveCatchAsync(Guid catchId, CancellationToken cancellationToken)
    {
        return tripCatches.RemoveAsync(catchId, cancellationToken);
    }
}
