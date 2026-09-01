using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;

public partial class OfflineCatchList : ComponentBase
{
    private IReadOnlyList<CatchModel> _catches = [];
    private Guid _ownerUserId;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _accessLocked;
    private TripModel? _activeTrip;
    private bool _isStartingTrip;

    [Inject] private ICatchStore CatchStore { get; set; } = default!;
    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IAnglerPreferencesStore AnglerPreferencesStore { get; set; } = default!;
    [Inject] private IActiveTripService ActiveTrip { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string ActiveTripHref
    {
        get
        {
            return _activeTrip is null ? "/offline/catches" : $"/offline/trips/{_activeTrip.Id:D}";
        }
    }

    protected override Task OnInitializedAsync()
    {
        return LoadAsync();
    }

    private async Task RetryLoadAsync()
    {
        if (_isLoading || _accessLocked)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        _accessLocked = false;
        try
        {
            var owner = OfflineOwnerContext.Owner;
            if (owner is null)
            {
                _accessLocked = true;
                _catches = [];
                return;
            }

            _ownerUserId = owner.UserId;
            _catches = LocalCatchVisibility.ForOwner(
                await CatchStore.GetAllAsync(owner.UserId, CancellationToken.None),
                owner.UserId);
            _activeTrip = await LoadActiveTripAsync(owner.UserId);
            var preferences = await AnglerPreferencesStore.GetAsync(owner.UserId, CancellationToken.None);
            if (preferences is not null)
            {
                _weightUnit = preferences.WeightUnit;
                _lengthUnit = preferences.LengthUnit;
            }
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading offline catches", exception, CancellationToken.None);
            _loadFailed = true;
            _catches = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<TripModel?> LoadActiveTripAsync(Guid ownerUserId)
    {
        try
        {
            return await ActiveTrip.GetActiveAsync(ownerUserId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("resolving the offline active trip", exception, CancellationToken.None);
            return null;
        }
    }

    private async Task StartFishingAsync()
    {
        if (_isStartingTrip || _activeTrip is not null || _ownerUserId == Guid.Empty)
        {
            return;
        }

        _isStartingTrip = true;
        try
        {
            var started = await ActiveTrip.StartAsync(_ownerUserId, CancellationToken.None);
            Navigation.NavigateTo($"/offline/trips/{started.Id:D}");
        }
        catch (TripAlreadyActiveException)
        {
            _activeTrip = await LoadActiveTripAsync(_ownerUserId);
            if (_activeTrip is not null)
            {
                Navigation.NavigateTo($"/offline/trips/{_activeTrip.Id:D}");
            }
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("starting an offline trip", exception, CancellationToken.None);
        }
        finally
        {
            _isStartingTrip = false;
        }
    }
}
