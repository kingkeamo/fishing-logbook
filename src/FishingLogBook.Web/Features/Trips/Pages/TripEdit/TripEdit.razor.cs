using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.TripEdit;

public partial class TripEdit : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private TripModel? _trip;
    private TripDisplayModel? _display;
    private IReadOnlyList<CatchModel> _catches = [];
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;
    private Guid _viewerUserId;

    [Parameter]
    public Guid TripId { get; set; }

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private ITripDisplayService TripDisplay { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string? SummaryLabel
    {
        get
        {
            if (_display?.StartedDate is null)
            {
                return null;
            }

            return _display.StartedTime is null
                ? _display.StartedDate
                : $"{_display.StartedDate} · {_display.StartedTime}";
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var ownerUserId = await LocalOwner.GetUserIdAsync(cancellationToken);
            _viewerUserId = ownerUserId;
            _trip = await TripStore.GetAsync(ownerUserId, TripId, cancellationToken);
            if (_trip is null)
            {
                return;
            }

            _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
            _catches = await LoadCatchesAsync(ownerUserId, cancellationToken);
            await ReadPreferencesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _trip = null;
            await Logging.LogErrorAsync("loading a trip for editing", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<IReadOnlyList<CatchModel>> LoadCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var catches = await CatchStore.GetMetadataAsync(ownerUserId, cancellationToken);
            return
            [
                .. catches
                    .Where(catchRecord => catchRecord.TripId == TripId)
                    .OrderBy(catchRecord => catchRecord.CaughtOn)
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading the catches of a trip for editing",
                exception,
                CancellationToken.None);
            return [];
        }
    }

    private async Task ReadPreferencesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var preferences = await AnglerPreferences.GetAsync(cancellationToken);
            _weightUnit = preferences.WeightUnit;
            _lengthUnit = preferences.LengthUnit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading angler preferences for a trip",
                exception,
                CancellationToken.None);
        }
    }

    private async Task RetryAsync()
    {
        if (_isLoading)
        {
            return;
        }

        await LoadAsync();
    }

    private void ReturnToDiary()
    {
        Navigation.NavigateTo($"/trips/{TripId:D}");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
