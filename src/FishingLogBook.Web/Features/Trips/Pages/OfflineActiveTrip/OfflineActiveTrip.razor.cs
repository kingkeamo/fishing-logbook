using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.OfflineActiveTrip;

public partial class OfflineActiveTrip : ComponentBase
{
    private TripModel? _trip;
    private TripDisplayModel? _display;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _isFinishing;
    private int? _catchCount;
    private int? _photographCount;
    private int? _noteCount;
    private IReadOnlyList<TripTimelineItemModel> _timeline = [];
    private IReadOnlyList<TripContributorDto> _contributors = [];
    private Guid _viewerUserId;

    [Parameter]
    public Guid TripId { get; set; }

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private IActiveTripService ActiveTrip { get; set; } = default!;

    [Inject]
    private ITripDisplayService TripDisplay { get; set; } = default!;

    [Inject]
    private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ITripTimelineService TripTimeline { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private async Task<IReadOnlyList<CatchModel>?> LoadCatchesAsync(Guid ownerUserId)
    {
        try
        {
            return await CatchStore.GetMetadataAsync(ownerUserId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading the catches of a trip offline",
                exception,
                CancellationToken.None);
            return null;
        }
    }

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

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            var owner = OfflineOwnerContext.Owner
                ?? throw new InvalidOperationException("Offline access is locked.");
            _viewerUserId = owner.UserId;
            _trip = await TripStore.GetAsync(owner.UserId, TripId, CancellationToken.None);
            if (_trip is not null)
            {
                _display = await TripDisplay.DescribeAsync(_trip, CancellationToken.None);
                var catches = await LoadCatchesAsync(owner.UserId);
                _catchCount = catches?.Count(catchRecord => catchRecord.TripId == TripId);
                _photographCount = _trip.Photographs.Count;
                _noteCount = _trip.Notes.Count;
                _timeline = TripTimeline.BuildLocal(_trip, catches ?? []);
            }
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _trip = null;
            await Logging.LogErrorAsync("loading a trip offline", exception, CancellationToken.None);
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

    private bool CanManageTrip
    {
        get
        {
            return !IsCompleted && _trip?.IsOwnedBy(_viewerUserId) == true;
        }
    }

    private bool CanAddPhotographs
    {
        get
        {
            return !IsCompleted && _trip?.CanContribute(_viewerUserId) == true;
        }
    }

    private bool CanRecordCatch => CanAddPhotographs;

    private async Task FinishAsync()
    {
        if (_trip is null || _isFinishing || IsCompleted || !_trip.IsOwnedBy(_viewerUserId))
        {
            return;
        }

        _isFinishing = true;
        try
        {
            _trip = await ActiveTrip.FinishAsync(_trip, CancellationToken.None);
            _display = await TripDisplay.DescribeAsync(_trip, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            await Logging.LogErrorAsync("finishing a trip offline", exception, CancellationToken.None);
        }
        finally
        {
            _isFinishing = false;
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
}
