using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripLocationPicker;

public partial class TripLocationPicker : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IReadOnlyList<FishingLocationPreferenceDto> _savedLocations = [];
    private string? _otherLocation;
    private string? _loadedPlaceName;
    private bool _hasLoadedPlaceName;

    [Parameter]
    public string? PlaceName { get; set; }

    [Parameter]
    public EventCallback<string?> PlaceNameChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<FishingLocationPreferenceDto> SavedLocations => _savedLocations;

    private bool HasPlace => !string.IsNullOrWhiteSpace(PlaceName);

    protected override async Task OnInitializedAsync()
    {
        await LoadSavedLocationsAsync();
    }

    protected override void OnParametersSet()
    {
        if (_hasLoadedPlaceName && _loadedPlaceName == PlaceName)
        {
            return;
        }

        _hasLoadedPlaceName = true;
        _loadedPlaceName = PlaceName;
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
        var place = TripConstants.TrimPlaceName(PlaceName);
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
        return FishingLocationConstants.AreSameName(PlaceName, name);
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
        if (string.Equals(place, TripConstants.TrimPlaceName(PlaceName), StringComparison.Ordinal))
        {
            return;
        }

        PlaceName = place;
        _loadedPlaceName = place;
        _otherLocation = ManualPlaceName();
        if (PlaceNameChanged.HasDelegate)
        {
            await PlaceNameChanged.InvokeAsync(place);
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
