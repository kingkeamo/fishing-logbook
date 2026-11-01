using FishingLogBook.Mobile.Features.Trips.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public static class TripModalViewModelServiceCollectionExtensions
{
    /// <summary>The modal view-models are transient and are only created through the factory, one per opening.</summary>
    public static IServiceCollection AddTripModalViewModels(this IServiceCollection services)
    {
        services.AddTransient<TripNoteEditorViewModel>();
        services.AddTransient<TripCatchPickerViewModel>();
        services.AddTransient<TripParticipantsViewModel>();
        services.AddTransient<StartPlannedTripViewModel>();
        services.AddSingleton<ITripModalViewModelFactory, TripModalViewModelFactory>();
        return services;
    }
}
