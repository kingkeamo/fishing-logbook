using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripRemovalService(
    ITripService trips,
    ITripCatchService tripCatches,
    InteractionService interactions,
    UiTextService text) : ITripRemovalService
{
    public async Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId, CancellationToken cancellationToken)
    {
        if (trip.FindNote(noteId) is not { } note
            || !await interactions.ConfirmAsync(new DialogRequest(
                text["TripNoteRemoveTitle"], text["TripNoteRemoveMessage"], text["TripNoteRemoveConfirm"], text["Cancel"], true), cancellationToken))
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
            await interactions.NotifyAsync(NotificationKindEnum.Error, text["TripNoteRemoveFailed"], cancellationToken);
            return false;
        }
    }

    public async Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel, CancellationToken cancellationToken)
    {
        if (!await interactions.ConfirmAsync(new DialogRequest(
                text["TripCatchRemoveTitle"], string.Format(text.Culture, text["TripCatchRemoveMessage"], catchLabel),
                text["TripCatchRemoveConfirm"], text["Cancel"], true), cancellationToken))
        {
            return false;
        }

        if (await RemoveFromTripAsync(catchId, cancellationToken))
        {
            return true;
        }

        await interactions.NotifyAsync(NotificationKindEnum.Error, text["TripEditSaveFailed"], cancellationToken);
        return false;
    }

    private async Task<bool> RemoveFromTripAsync(Guid catchId, CancellationToken cancellationToken)
    {
        try
        {
            return await tripCatches.RemoveAsync(catchId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }
}
