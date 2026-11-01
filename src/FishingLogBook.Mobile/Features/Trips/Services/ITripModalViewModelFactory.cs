using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Services;

/// <summary>Gives every modal opening its own view-model, so two openings never share state.</summary>
public interface ITripModalViewModelFactory
{
    TripNoteEditorViewModel CreateNoteEditor();

    TripCatchPickerViewModel CreateCatchPicker();

    TripParticipantsViewModel CreateParticipants();
}
