using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

/// <summary>
/// Opens the trip modals on behalf of the trip components, like the Web components open their own modals.
/// Every method returns true when something changed, so the owning screen only has to refresh. Each opening gets its
/// own view-model from the factory. Confirming and removing belongs to <see cref="ITripRemovalService"/>.
/// </summary>
public sealed class TripModalService(ITripRemovalService removal, ITripModalViewModelFactory viewModels) : ITripModalService
{
    public async Task<bool> EditNoteAsync(LocalTripRecordModel trip, Guid? noteId)
    {
        var editor = viewModels.CreateNoteEditor();
        return editor.Initialize(trip, noteId) && await ShowAsync(new TripNoteModal(editor));
    }

    public Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId)
    {
        return removal.RemoveNoteAsync(trip, noteId, CancellationToken.None);
    }

    public async Task<bool> AddCatchesAsync(LocalTripRecordModel trip)
    {
        var picker = viewModels.CreateCatchPicker();
        using var cancellation = new CancellationTokenSource();
        return await ShowWhileLoadingAsync(new TripCatchPickerModal(picker), picker.InitializeAsync(trip, cancellation.Token), cancellation);
    }

    public Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel)
    {
        return removal.RemoveCatchAsync(catchId, catchLabel, CancellationToken.None);
    }

    public async Task<bool> ShowParticipantsAsync(Guid tripId)
    {
        var participants = viewModels.CreateParticipants();
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
