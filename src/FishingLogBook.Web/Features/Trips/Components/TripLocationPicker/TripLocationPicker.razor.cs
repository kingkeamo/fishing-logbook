using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripLocationPicker;

public partial class TripLocationPicker : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IReadOnlyList<FishingLocationPreferenceDto> _savedLocations = [];
    private string? _otherLocation;
    private Guid _loadedTripId;
    private bool _isSaving;
    private bool _saveFailed;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public EventCallback<TripModel> OnPlaceChanged { get; set; }

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private IActiveTripService ActiveTrip { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<FishingLocationPreferenceDto> SavedLocations => _savedLocations;

    private bool HasPlace => !string.IsNullOrWhiteSpace(Trip.PlaceName);

    protected override async Task OnInitializedAsync()
    {
        await LoadSavedLocationsAsync();
    }

    protected override void OnParametersSet()
    {
        if (_loadedTripId == Trip.Id)
        {
            return;
        }

        _loadedTripId = Trip.Id;
        _otherLocation = ManualPlaceName();
    }

    private async Task LoadSavedLocationsAsync()
    {
        try
        {
            var preferences = await AnglerPreferences.GetAsync(_cancellationTokenSource.Token);
            _savedLocations = preferences.Locations;
            _otherLocation = ManualPlaceName();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading saved fishing locations", exception, CancellationToken.None);
        }
    }

    private string? ManualPlaceName()
    {
        var place = TripConstants.TrimPlaceName(Trip.PlaceName);
        if (place is null)
        {
            return null;
        }

        return _savedLocations.Any(location => FishingLocationConstants.AreSameName(location.Name, place))
            ? null
            : place;
    }

    private bool IsSelected(string name)
    {
        return FishingLocationConstants.AreSameName(Trip.PlaceName, name);
    }

    private async Task SelectAsync(string name)
    {
        await ApplyAsync(name);
    }

    private async Task OnOtherChangedAsync(string? value)
    {
        _otherLocation = value;
        await ApplyAsync(value);
    }

    private async Task ClearAsync()
    {
        _otherLocation = null;
        await ApplyAsync(null);
    }

    private async Task ApplyAsync(string? placeName)
    {
        var place = TripConstants.TrimPlaceName(placeName);
        if (string.Equals(place, TripConstants.TrimPlaceName(Trip.PlaceName), StringComparison.Ordinal))
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            var updated = await ActiveTrip.UpdatePlaceAsync(Trip, place, _cancellationTokenSource.Token);
            if (updated is null)
            {
                _saveFailed = true;
                return;
            }

            Trip = updated;
            _otherLocation = ManualPlaceName();
            if (OnPlaceChanged.HasDelegate)
            {
                await OnPlaceChanged.InvokeAsync(updated);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _saveFailed = true;
            await Logging.LogErrorAsync("updating the trip fishing location", exception, CancellationToken.None);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static string ChoiceId(string name)
    {
        var characters = name
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-');
        return $"trip-location-choice-{new string([.. characters])}";
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
