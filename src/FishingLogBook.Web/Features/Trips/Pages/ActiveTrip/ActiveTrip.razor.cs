using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
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
    private int? _catchCount;
    private IReadOnlyList<TripTimelineItemModel> _timeline = [];
    private int? _photographCount;
    private int? _noteCount;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isReadOnlyHistory;
    private bool _isOnline = true;

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
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private INetworkService Network { get; set; } = default!;

    [Inject]
    private ITripTimelineService TripTimeline { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

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

    private bool CanEdit
    {
        get
        {
            return !IsCompleted && !_isReadOnlyHistory;
        }
    }

    private bool CanContribute
    {
        get
        {
            return !_isReadOnlyHistory || _isOnline;
        }
    }

    private bool CanAddNotes => CanContribute;

    private bool CanAddCatches => CanContribute;

    private TripStorageEnum NoteStorage
    {
        get
        {
            return _isReadOnlyHistory ? TripStorageEnum.Server : TripStorageEnum.LocalFirst;
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

    protected override void OnInitialized()
    {
        Network.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await RefreshConnectivityAsync();
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        _ = UpdateConnectivityAsync(isOnline);
    }

    private async Task UpdateConnectivityAsync(bool isOnline)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _isOnline = isOnline;
            StateHasChanged();
        });
    }

    private async Task RefreshConnectivityAsync()
    {
        try
        {
            _isOnline = await Network.IsOnlineAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading connectivity for a trip",
                exception,
                CancellationToken.None);
        }
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

    private async Task<IReadOnlyList<CatchModel>?> LoadCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CatchStore.GetMetadataAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading the catches of a trip", exception, CancellationToken.None);
            return null;
        }
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
                _isReadOnlyHistory = false;
                _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
                var catches = await LoadCatchesAsync(ownerUserId, cancellationToken);
                _catchCount = catches?.Count(catchRecord => catchRecord.TripId == TripId);
                _photographCount = _trip.Photographs.Count;
                _noteCount = _trip.Notes.Count;
                _timeline = TripTimeline.BuildLocal(_trip, catches ?? []);
                await ReadPreferencesAsync(cancellationToken);
                return;
            }

            await LoadHistoricalAsync(ownerUserId, cancellationToken);
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

    private async Task LoadHistoricalAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var detail = await TripClient.GetDetailAsync(TripId, cancellationToken);
        if (detail is null)
        {
            return;
        }

        _isReadOnlyHistory = true;
        _trip = ToTripModel(detail.Trip, ownerUserId);
        _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
        _catchCount = detail.Catches.Count;
        _photographCount = detail.Photographs.Count;
        _noteCount = detail.Notes.Count;
        _timeline = TripTimeline.BuildRemote(detail);
        await ReadPreferencesAsync(cancellationToken);
    }

    private static TripModel ToTripModel(TripViewDto view, Guid ownerUserId)
    {
        return new TripModel(
            view.Id,
            ownerUserId,
            view.Status,
            view.StartedOn,
            view.EndedOn,
            view.Title,
            view.PlaceName,
            SyncStatus: SyncStatus.Synchronised);
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
        Network.ConnectivityChanged -= OnConnectivityChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
