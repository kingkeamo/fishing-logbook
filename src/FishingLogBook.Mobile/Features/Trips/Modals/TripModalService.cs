using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

/// <summary>
/// Opens the trip modals and confirmations on behalf of the trip components, like the Web components open their
/// own modals. Every method returns true when something changed, so the owning screen only has to refresh.
/// Saving and removing trip data belongs to the view-models and <see cref="ITripRemovalService"/>.
/// </summary>
public sealed class TripModalService(
    ITripRemovalService removal,
    TripNoteEditorViewModel noteEditor,
    TripCatchPickerViewModel catchPicker,
    TripParticipantsViewModel participants,
    InteractionService interactions,
    UiTextService text) : ITripModalService
{
    public async Task<bool> EditNoteAsync(LocalTripRecordModel trip, Guid? noteId)
    {
        return noteEditor.Initialize(trip, noteId) && await ShowAsync(new TripNoteModal(noteEditor));
    }

    public async Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId)
    {
        if (trip.FindNote(noteId) is null
            || !await interactions.ConfirmAsync(new DialogRequest(
                text["TripNoteRemoveTitle"], text["TripNoteRemoveMessage"], text["TripNoteRemoveConfirm"], text["Cancel"], true)))
        {
            return false;
        }

        if (await removal.RemoveNoteAsync(trip, noteId, CancellationToken.None))
        {
            return true;
        }

        await interactions.NotifyAsync(NotificationKindEnum.Error, text["TripNoteRemoveFailed"]);
        return false;
    }

    public async Task<bool> AddCatchesAsync(LocalTripRecordModel trip)
    {
        using var cancellation = new CancellationTokenSource();
        return await ShowWhileLoadingAsync(new TripCatchPickerModal(catchPicker), catchPicker.InitializeAsync(trip, cancellation.Token), cancellation);
    }

    public async Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel)
    {
        if (!await interactions.ConfirmAsync(new DialogRequest(
                text["TripCatchRemoveTitle"], string.Format(text.Culture, text["TripCatchRemoveMessage"], catchLabel),
                text["TripCatchRemoveConfirm"], text["Cancel"], true)))
        {
            return false;
        }

        if (await removal.RemoveCatchAsync(catchId, CancellationToken.None))
        {
            return true;
        }

        await interactions.NotifyAsync(NotificationKindEnum.Error, text["TripEditSaveFailed"]);
        return false;
    }

    public async Task<bool> ShowParticipantsAsync(Guid tripId)
    {
        using var cancellation = new CancellationTokenSource();
        var changed = await ShowWhileLoadingAsync(new TripParticipantsModal(participants), participants.LoadAsync(tripId, cancellation.Token), cancellation);
        return changed || participants.Changed;
    }

    private static async Task<bool> ShowAsync(Popup<bool> popup)
    {
        var result = await Shell.Current.ShowPopupAsync<bool>(popup);
        return !result.WasDismissedByTappingOutsideOfPopup && result.Result;
    }

    /// <summary>
    /// The view-model has already started loading (and shows its loading state), so the popup never opens on stale content.
    /// Dismissing the popup cancels a load still in flight; the view-models report their own load failures.
    /// </summary>
    private static async Task<bool> ShowWhileLoadingAsync(Popup<bool> popup, Task loading, CancellationTokenSource cancellation)
    {
        var changed = await ShowAsync(popup);
        await cancellation.CancelAsync();
        try
        {
            await loading;
        }
        catch (OperationCanceledException)
        {
        }

        return changed;
    }
}
