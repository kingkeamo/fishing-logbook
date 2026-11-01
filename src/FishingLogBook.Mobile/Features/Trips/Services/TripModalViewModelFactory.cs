using FishingLogBook.Mobile.Features.Trips.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripModalViewModelFactory(IServiceProvider services) : ITripModalViewModelFactory
{
    public TripNoteEditorViewModel CreateNoteEditor()
    {
        return services.GetRequiredService<TripNoteEditorViewModel>();
    }

    public TripCatchPickerViewModel CreateCatchPicker()
    {
        return services.GetRequiredService<TripCatchPickerViewModel>();
    }

    public StartPlannedTripViewModel CreateStartPlannedTrip()
    {
        return services.GetRequiredService<StartPlannedTripViewModel>();
    }

    public TripParticipantsViewModel CreateParticipants()
    {
        return services.GetRequiredService<TripParticipantsViewModel>();
    }
}
