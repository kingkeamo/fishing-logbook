using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

/// <summary>
/// Opens the trip modals and confirmations on behalf of the trip components, like the Web components open their
/// own modals. Every method returns true when something changed, so the owning screen only has to refresh.
/// </summary>
public sealed class TripModalService(
    ITripService trips,
    ITripCatchService tripCatches,
    TripNoteEditorViewModel noteEditor,
    TripCatchPickerViewModel catchPicker,
    TripParticipantsViewModel participants,
    InteractionService interactions,
    UiTextService text) : ITripModalService
{
    public async Task<bool> EditNoteAsync(LocalTripRecordModel trip, TripNoteDto? existing)
    {
        noteEditor.Initialize(trip, existing);
        return await ShowAsync(new TripNoteModal(noteEditor));
    }

    public async Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId)
    {
        if (trip.Detail.Notes.FirstOrDefault(note => note.Id == noteId) is not { } note
            || !await interactions.ConfirmAsync(new DialogRequest(
                text["TripNoteRemoveTitle"], text["TripNoteRemoveMessage"], text["TripNoteRemoveConfirm"], text["Cancel"], true)))
        {
            return false;
        }

        try
        {
            await trips.SaveNoteAsync(trip, note, deleted: true, CancellationToken.None);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await interactions.NotifyAsync(NotificationKindEnum.Error, text["TripNoteRemoveFailed"]);
            return false;
        }
    }

    public async Task<bool> AddCatchesAsync(LocalTripRecordModel trip)
    {
        var pending = ShowAsync(new TripCatchPickerModal(catchPicker));
        await catchPicker.InitializeAsync(trip, CancellationToken.None);
        return await pending;
    }

    public async Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel)
    {
        if (!await interactions.ConfirmAsync(new DialogRequest(
                text["TripCatchRemoveTitle"], string.Format(text.Culture, text["TripCatchRemoveMessage"], catchLabel),
                text["TripCatchRemoveConfirm"], text["Cancel"], true)))
        {
            return false;
        }

        if (await tripCatches.RemoveAsync(catchId, CancellationToken.None))
        {
            return true;
        }

        await interactions.NotifyAsync(NotificationKindEnum.Error, text["TripEditSaveFailed"]);
        return false;
    }

    public async Task<bool> ShowParticipantsAsync(Guid tripId)
    {
        var pending = ShowAsync(new TripParticipantsModal(participants));
        await participants.LoadAsync(tripId, CancellationToken.None);
        return await pending || participants.Changed;
    }

    private static async Task<bool> ShowAsync(Popup<bool> popup)
    {
        var result = await Shell.Current.ShowPopupAsync<bool>(popup);
        return !result.WasDismissedByTappingOutsideOfPopup && result.Result;
    }
}
