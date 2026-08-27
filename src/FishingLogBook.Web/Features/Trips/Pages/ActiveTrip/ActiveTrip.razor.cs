using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.ActiveTrip;

public partial class ActiveTrip : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private TripModel? _trip;
    private TripDisplayModel? _display;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _isFinishing;
    private bool _locationAttempted;

    [Parameter]
    public Guid TripId { get; set; }

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private IActiveTripService ActiveTripService { get; set; } = default!;

    [Inject]
    private ITripDisplayService TripDisplay { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool IsCompleted
    {
        get
        {
            return _trip?.Status == TripConstants.Completed;
        }
    }

    private string? GeneratedTitle
    {
        get
        {
            return _display?.StartedDate;
        }
    }

    private string? StartedLabel
    {
        get
        {
            return _display?.StartedTime;
        }
    }

    private string? DurationLabel
    {
        get
        {
            return _display?.Elapsed is null ? null : FormatDuration(_display.Elapsed.Value);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _locationAttempted || _trip is null || IsCompleted)
        {
            return;
        }

        _locationAttempted = true;
        await TryAttachLocationAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var ownerUserId = await LocalOwner.GetUserIdAsync(cancellationToken);
            _trip = await TripStore.GetAsync(ownerUserId, TripId, cancellationToken);
            if (_trip is not null)
            {
                _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _trip = null;
            await Logging.LogErrorAsync("loading a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
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

    private async Task FinishAsync()
    {
        if (_trip is null || _isFinishing || IsCompleted)
        {
            return;
        }

        _isFinishing = true;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            _trip = await ActiveTripService.FinishAsync(_trip, cancellationToken);
            _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            await Logging.LogErrorAsync("finishing a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isFinishing = false;
        }
    }

    private async Task TryAttachLocationAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var located = await ActiveTripService.TryAttachLocationAsync(_trip!, cancellationToken);
            if (located is null)
            {
                return;
            }

            _trip = located;
            StateHasChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("attaching a trip location", exception, CancellationToken.None);
        }
    }

    private string FormatDuration(TimeSpan elapsed)
    {
        var hours = (int)elapsed.TotalHours;
        var minutes = elapsed.Minutes;
        return hours > 0
            ? Loc["Trip_DurationHoursMinutes", hours, minutes]
            : Loc["Trip_DurationMinutes", minutes];
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
